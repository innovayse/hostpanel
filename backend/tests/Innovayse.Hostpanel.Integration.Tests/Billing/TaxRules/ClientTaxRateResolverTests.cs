using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>
/// <see cref="ClientTaxRateResolver"/> against Postgres: the one place that decides the rate a new invoice for a client
/// is raised at. Every test uses its own country, so the rules of one test never reach another's client.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientTaxRateResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Resolves the rate for a client.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The resolver's answer.</returns>
    private async Task<decimal> RateAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ClientTaxRateResolver>();
        var result = await resolver.ForClientAsync(clientId, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    /// <summary>A client whose country has no rule is not taxed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoRule_GivesZero()
    {
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "GR");

        (await RateAsync(clientId)).Should().Be(0m);
    }

    /// <summary>A client with no country is not taxed, even where a rule exists for every other country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoCountry_GivesZero()
    {
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, null);

        (await RateAsync(clientId)).Should().Be(0m);
    }

    /// <summary>The enabled country-wide rule of the client's country is the rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountryRule_GivesItsRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "FI", null, 25.5m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "fi", "Uusimaa");

        (await RateAsync(clientId)).Should().Be(25.5m);
    }

    /// <summary>The rule of the client's region wins over the country-wide rule, whatever the case of the state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegionRule_BeatsTheCountryRule()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "CA", null, 5m);
        await TaxRuleTestSeeds.CreateAsync(admin, "CA", "Quebec", 14.975m);
        var inRegion = await TaxRuleTestSeeds.SeedClientAsync(factory, "CA", "QUEBEC");
        var elsewhere = await TaxRuleTestSeeds.SeedClientAsync(factory, "CA", "Alberta");
        var noState = await TaxRuleTestSeeds.SeedClientAsync(factory, "CA");

        (await RateAsync(inRegion)).Should().Be(14.975m);
        (await RateAsync(elsewhere)).Should().Be(5m);
        (await RateAsync(noState)).Should().Be(5m);
    }

    /// <summary>A region with only a region rule leaves the rest of the country untaxed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegionRuleOnly_DoesNotTaxTheRestOfTheCountry()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "JP", "Tokyo", 10m);
        var inRegion = await TaxRuleTestSeeds.SeedClientAsync(factory, "JP", "Tokyo");
        var elsewhere = await TaxRuleTestSeeds.SeedClientAsync(factory, "JP", "Osaka");

        (await RateAsync(inRegion)).Should().Be(10m);
        (await RateAsync(elsewhere)).Should().Be(0m);
    }

    /// <summary>A disabled rule is skipped: a disabled region rule falls back to the country-wide rule, a disabled country rule to none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledRule_IsIgnored()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "DK", null, 25m);
        await TaxRuleTestSeeds.CreateAsync(admin, "DK", "Nordjylland", 12m, isEnabled: false);
        await TaxRuleTestSeeds.CreateAsync(admin, "NO", null, 25m, isEnabled: false);
        var region = await TaxRuleTestSeeds.SeedClientAsync(factory, "DK", "Nordjylland");
        var disabledCountry = await TaxRuleTestSeeds.SeedClientAsync(factory, "NO");

        (await RateAsync(region)).Should().Be(25m);
        (await RateAsync(disabledCountry)).Should().Be(0m);
    }

    /// <summary>A tax exempt client is never taxed, whatever rule the country has.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TaxExemptClient_GivesZero()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "IE", null, 23m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "IE", taxExempt: true);

        (await RateAsync(clientId)).Should().Be(0m);
    }

    /// <summary>With the global switch off no client is taxed; switching it on again restores the rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SwitchOff_GivesZero()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "PL", null, 23m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "PL");

        await TaxRuleTestSeeds.SetTaxEnabledAsync(factory, false);
        try
        {
            (await RateAsync(clientId)).Should().Be(0m);
        }
        finally
        {
            await TaxRuleTestSeeds.SetTaxEnabledAsync(factory, true);
        }

        (await RateAsync(clientId)).Should().Be(23m);
    }

    /// <summary>An id that names no client is refused with the client-not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownClient_IsRefused()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ClientTaxRateResolver>();

        var result = await resolver.ForClientAsync(2147483000, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
