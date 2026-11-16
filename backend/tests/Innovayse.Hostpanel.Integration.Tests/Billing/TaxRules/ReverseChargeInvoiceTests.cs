using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>
/// The reverse charge on a new invoice, through the admin API against Postgres. The seller is in Malta for the whole class
/// (a setting that is global, so every test that depends on it lives here and the class runs one test at a time), with
/// tax rules for Malta, Cyprus and Norway; the clients differ in country, number and the verdict recorded for it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
[Collection(TaxSettingsCollection.Name)]
public sealed class ReverseChargeInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>The country the seller is established in for these tests.</summary>
    private const string SellerCountry = "MT";

    /// <summary>The line price of every invoice, so the expected totals are easy to read.</summary>
    private const decimal Price = 100m;

    /// <summary>Lets one test at a time create the shared tax rules.</summary>
    private static readonly SemaphoreSlim RuleGate = new(1, 1);

    /// <summary>Whether the tax rules of this class exist already; they are the same for every test.</summary>
    private static bool rulesCreated;

    /// <summary>The seller's country before the test, put back afterwards.</summary>
    private string previousSeller = string.Empty;

    /// <summary>Makes the seller Maltese and makes sure the tax rules exist.</summary>
    /// <returns>A task that completes when the setup is done.</returns>
    public async Task InitializeAsync()
    {
        previousSeller = await VatRateTestSeeds.SetSellerCountryAsync(factory, SellerCountry);
        await RuleGate.WaitAsync();
        try
        {
            if (!rulesCreated)
            {
                var admin = await BillingTestSeeds.AdminClientAsync(factory);
                await TaxRuleTestSeeds.CreateAsync(admin, "MT", null, 18m);
                await TaxRuleTestSeeds.CreateAsync(admin, "CY", null, 19m);
                await TaxRuleTestSeeds.CreateAsync(admin, "NO", null, 25m);
                rulesCreated = true;
            }
        }
        finally
        {
            RuleGate.Release();
        }
    }

    /// <summary>Puts the seller's country back.</summary>
    /// <returns>A task that completes when the setting is restored.</returns>
    public async Task DisposeAsync()
    {
        await VatRateTestSeeds.SetSellerCountryAsync(factory, previousSeller);
    }

    /// <summary>
    /// A new invoice is without VAT and states the reverse charge for a business with a valid number in another EU country or
    /// the UK; the same client with an invalid number, one never judged (the service had no answer), no number, or in the
    /// seller's own country or a non-EU country is taxed as before.
    /// </summary>
    /// <param name="country">The client's country.</param>
    /// <param name="taxId">The client's VAT number, or null for none.</param>
    /// <param name="status">The recorded verdict, or null for a number never judged.</param>
    /// <param name="reverseCharge">Whether the invoice is under the reverse charge.</param>
    /// <param name="taxRate">The rate the invoice carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("CY", "CY10259033P", VatCheckOutcome.Valid, true, 0)]
    [InlineData("GB", "GB123456789", VatCheckOutcome.Valid, true, 0)]
    [InlineData("MT", "MT12345678", VatCheckOutcome.Valid, false, 18)]
    [InlineData("NO", "NO923456789MVA", VatCheckOutcome.Valid, false, 25)]
    [InlineData("CY", null, null, false, 19)]
    [InlineData("CY", "CY10259033P", VatCheckOutcome.Invalid, false, 19)]
    [InlineData("CY", "CY10259033P", null, false, 19)]
    public async Task NewInvoice_IsTreatedByTheClientsCountryNumberAndVerdict(
        string country, string? taxId, VatCheckOutcome? status, bool reverseCharge, int taxRate)
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, country, taxId, status);

        var invoice = await CreateInvoiceAsync(clientId);

        invoice.GetProperty("reverseCharge").GetBoolean().Should().Be(reverseCharge);
        invoice.GetProperty("clientVatNumber").GetString().Should().Be(reverseCharge ? taxId : null);
        var amounts = invoice.GetProperty("amounts");
        amounts.GetProperty("taxRate").GetDecimal().Should().Be(taxRate);
        amounts.GetProperty("tax").GetDecimal().Should().Be(Price * taxRate / 100m);
        amounts.GetProperty("total").GetDecimal().Should().Be(Price + (Price * taxRate / 100m));
    }

    /// <summary>The manual tax exemption keeps working and wins: no tax, and no reverse charge statement either.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TaxExemptClient_IsNotTaxedAndStatesNoReverseCharge()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Valid, taxExempt: true);

        var invoice = await CreateInvoiceAsync(clientId);

        invoice.GetProperty("reverseCharge").GetBoolean().Should().BeFalse();
        invoice.GetProperty("amounts").GetProperty("tax").GetDecimal().Should().Be(0m);
    }

    /// <summary>Without a seller country there is nothing to be "another country" from, so the client is taxed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithNoSellerCountry_TheClientIsTaxed()
    {
        await VatRateTestSeeds.SetSellerCountryAsync(factory, string.Empty);
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Valid);

        var invoice = await CreateInvoiceAsync(clientId);

        invoice.GetProperty("reverseCharge").GetBoolean().Should().BeFalse();
        invoice.GetProperty("amounts").GetProperty("taxRate").GetDecimal().Should().Be(19m);
    }

    /// <summary>
    /// A check the service could not answer keeps the status the client had, so a client that was valid is still invoiced
    /// under the reverse charge, while one that was never judged stays taxed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnavailableCheck_KeepsThePreviousTreatment()
    {
        var valid = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Valid);
        var unjudged = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P");
        var host = VatTestSeeds.HostWith(factory, new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable)));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        (await admin.PostAsync($"/api/clients/{valid}/vat-check", content: null)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await admin.PostAsync($"/api/clients/{unjudged}/vat-check", content: null)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var keptValid = await CreateInvoiceAsync(valid);
        var stillUnjudged = await CreateInvoiceAsync(unjudged);

        keptValid.GetProperty("reverseCharge").GetBoolean().Should().BeTrue();
        stillUnjudged.GetProperty("reverseCharge").GetBoolean().Should().BeFalse();
        stillUnjudged.GetProperty("amounts").GetProperty("taxRate").GetDecimal().Should().Be(19m);
    }

    /// <summary>A number that stops being valid stops the reverse charge for new invoices, and the invoices already raised keep it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANumberThatStopsBeingValid_StopsTheReverseChargeForNewInvoicesOnly()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Valid);
        var earlier = await CreateInvoiceAsync(clientId);
        var host = VatTestSeeds.HostWith(factory, new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Invalid)));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        (await admin.PostAsync($"/api/clients/{clientId}/vat-check", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var later = await CreateInvoiceAsync(clientId);

        later.GetProperty("reverseCharge").GetBoolean().Should().BeFalse();
        later.GetProperty("amounts").GetProperty("taxRate").GetDecimal().Should().Be(19m);
        var reread = await ReadInvoiceAsync(earlier.GetProperty("id").GetInt32());
        reread.GetProperty("reverseCharge").GetBoolean().Should().BeTrue();
    }

    /// <summary>A rate typed by hand is refused on the invoice of a client invoiced under the reverse charge; zero is accepted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptions_RefusesAHandTypedRateForAReverseChargeClient()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Valid);
        var invoiceId = (await CreateInvoiceAsync(clientId)).GetProperty("id").GetInt32();
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var refused = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", Options(10m));
        var zero = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", Options(0m));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.CLIENT_REVERSE_CHARGE_APPLIES);
        zero.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reread = await ReadInvoiceAsync(invoiceId);
        reread.GetProperty("reverseCharge").GetBoolean().Should().BeTrue();
        reread.GetProperty("amounts").GetProperty("tax").GetDecimal().Should().Be(0m);
    }

    /// <summary>A client who is taxed can still be given a rate by hand, as before.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptions_StillAcceptsAHandTypedRateForATaxedClient()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "CY", "CY10259033P", VatCheckOutcome.Invalid);
        var invoiceId = (await CreateInvoiceAsync(clientId)).GetProperty("id").GetInt32();
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", Options(10m));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadInvoiceAsync(invoiceId)).GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(110m);
    }

    /// <summary>The body of the invoice options route with the given rate.</summary>
    /// <param name="taxRate">The rate to send.</param>
    /// <returns>The request body.</returns>
    private static object Options(decimal taxRate) => new
    {
        invoiceDate = TestMoments.Seeded,
        dueDate = TestMoments.Seeded.AddDays(7),
        paymentMethod = PaymentMethodNames.Manual,
        taxRate,
    };

    /// <summary>Raises an invoice of one line for the client and reads it back.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The invoice as the admin API answers it.</returns>
    private async Task<JsonElement> CreateInvoiceAsync(int clientId)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, Price);
        return await ReadInvoiceAsync(invoiceId);
    }

    /// <summary>Reads an invoice through the admin API.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice as the API answers it.</returns>
    private async Task<JsonElement> ReadInvoiceAsync(int invoiceId)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        return await admin.GetFromJsonAsync<JsonElement>($"/api/billing/{invoiceId}");
    }
}
