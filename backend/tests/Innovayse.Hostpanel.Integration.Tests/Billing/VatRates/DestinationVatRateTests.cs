using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>
/// <see cref="ClientTaxRateResolver"/> against Postgres, for the VAT rate of the client's country: a consumer in an EU member
/// state other than the seller's is invoiced for digital services at that country's standard rate in force on the invoice
/// date, and every other invoice keeps the rate of the tax rules. Every test uses its own countries, so the rules and the
/// seller setting one test changes never reach another's client.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DestinationVatRateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Resolves the rate of an invoice, with the seller's country set for the call.</summary>
    /// <param name="sellerCountry">The seller's country to store, or empty for none.</param>
    /// <param name="invoice">The invoice to resolve.</param>
    /// <returns>The rate, in percent.</returns>
    private async Task<decimal> RateAsync(string sellerCountry, Invoice invoice)
    {
        await VatRateTestSeeds.SetSellerCountryAsync(factory, sellerCountry);
        try
        {
            using var scope = factory.Services.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<ClientTaxRateResolver>();
            var result = await resolver.ForInvoiceAsync(invoice, CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            return result.Value;
        }
        finally
        {
            await VatRateTestSeeds.SetSellerCountryAsync(factory, string.Empty);
        }
    }

    /// <summary>A consumer in another member state is invoiced for a digital service at the standard rate of their own country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConsumerInAnotherMemberState_GetsTheirCountrysStandardRate()
    {
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "fr");

        var rate = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true, true));

        rate.Should().Be(20m);
    }

    /// <summary>A consumer in the seller's own country keeps the rate of the tax rules, whatever the VAT rate table holds for that country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConsumerInTheSellersCountry_KeepsTheTaxRuleRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "BG", null, 5m);
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "BG");

        var rate = await RateAsync("BG", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true));

        rate.Should().Be(5m);
    }

    /// <summary>The rate is the one in force on the invoice date: a rate change moves it from its first day on, not before.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheRateInForceOnTheInvoiceDate_IsTheOneApplied()
    {
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "SK");
        var beforeChange = new DateTimeOffset(2024, 12, 31, 23, 0, 0, TimeSpan.Zero);
        var onChange = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var before = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, beforeChange, true));
        var after = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, onChange, true));

        before.Should().Be(20m);
        after.Should().Be(23m);
    }

    /// <summary>A row staff add with a later first day takes over from that day, and the day before still reads the earlier rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARateAddedForALaterDay_TakesOverFromThatDay()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await VatRateTestSeeds.CreateAsync(admin, "HR", "Standard", 26m, new DateOnly(2050, 1, 1));
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ClientTaxRateResolver>();

        var before = await resolver.StandardVatRateAsync("HR", new DateOnly(2049, 12, 31), CancellationToken.None);
        var onTheDay = await resolver.StandardVatRateAsync("HR", new DateOnly(2050, 1, 1), CancellationToken.None);

        before.Should().Be(25m);
        onTheDay.Should().Be(26m);
    }

    /// <summary>A date before the first rate on record has no rate, so the invoice falls back to the tax rules.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADateBeforeTheFirstRate_HasNoVatRate()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ClientTaxRateResolver>();
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "HU");

        var lookup = await resolver.StandardVatRateAsync("HU", new DateOnly(2020, 1, 1), CancellationToken.None);
        var rate = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), true));

        lookup.Should().BeNull();
        rate.Should().Be(0m);
    }

    /// <summary>A reduced rate is never applied on its own: the standard rate is the one a digital service is taxed at.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AReducedRate_IsNotUsed()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await VatRateTestSeeds.CreateAsync(admin, "LV", "Reduced", 12m, new DateOnly(2025, 1, 1));
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "LV");

        var rate = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true));

        rate.Should().Be(21m);
    }

    /// <summary>One line that is not a digital service puts the whole invoice back on the tax rules, since an invoice carries one rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnInvoiceWithAnyOtherLine_KeepsTheTaxRuleRate()
    {
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "LT");

        var mixed = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true, false));
        var none = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, false));
        var noLines = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded));

        mixed.Should().Be(0m);
        none.Should().Be(0m);
        noLines.Should().Be(0m);
    }

    /// <summary>A client with a tax identification number is a business: the destination rate is not applied to it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ABusinessClient_KeepsTheTaxRuleRate()
    {
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "EE", "EE123456789");

        var rate = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true));

        rate.Should().Be(0m);
    }

    /// <summary>With no seller country set the destination rate is off, and the tax rules alone decide.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithNoSellerCountry_TheTaxRulesDecide()
    {
        var clientId = await VatRateTestSeeds.SeedClientAsync(factory, "CY");

        var rate = await RateAsync(string.Empty, VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true));

        rate.Should().Be(0m);
    }

    /// <summary>A tax exempt consumer is never taxed, the destination rate included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ATaxExemptClient_IsNotTaxed()
    {
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "RO", taxExempt: true);

        var rate = await RateAsync("DE", VatRateTestSeeds.InvoiceOf(clientId, TestMoments.Seeded, true));

        rate.Should().Be(0m);
    }

    /// <summary>The digital service flag of a product reaches the invoice line through the billed snapshot, and defaults to off.</summary>
    [Fact]
    public void TheDigitalServiceFlag_ComesFromTheLineSnapshot()
    {
        var invoice = VatRateTestSeeds.InvoiceOf(1, TestMoments.Seeded, true, false);
        invoice.AddItem("Manual line", 5m, 1);

        invoice.Items.Select(i => i.IsDigitalService).Should().Equal(true, false, false);
        new ProductSnapshot(1, BillingCycle.Monthly, false, null, null).IsDigitalService.Should().BeFalse();
    }
}
