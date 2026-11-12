using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>Integration tests for the <c>CreateTaxRule</c> use case: <c>POST /api/admin/tax-rules</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTaxRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a rule answers 201 with its id, and the rule reads back with an upper-case country and a trimmed region.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_StoresTheRule()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body("us", " California ", 7.25m, name: "California sales tax"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        var rule = await TaxRuleTestSeeds.GetAsync(admin, id);
        rule.GetProperty("name").GetString().Should().Be("California sales tax");
        rule.GetProperty("country").GetString().Should().Be("US");
        rule.GetProperty("region").GetString().Should().Be("California");
        rule.GetProperty("ratePercent").GetDecimal().Should().Be(7.25m);
        rule.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
    }

    /// <summary>A second rule for the same country and region is refused with 409, whatever the case of the region.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_SamePlaceTwice_RefusesAsAlreadyExisting()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "FR", "Bretagne");
        await TaxRuleTestSeeds.CreateAsync(admin, "FR");

        var sameRegion = await admin.PostAsJsonAsync(TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body("FR", "BRETAGNE"));
        var sameCountry = await admin.PostAsJsonAsync(TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body("fr"));

        sameRegion.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(sameRegion)).Should().Be(HostpanelErrorCodes.TAX_RULE_ALREADY_EXISTS);
        sameCountry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(sameCountry)).Should().Be(HostpanelErrorCodes.TAX_RULE_ALREADY_EXISTS);
    }

    /// <summary>A country-wide rule and a region rule of the same country live side by side.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RegionBesideCountryWideRule_IsAllowed()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "IT");

        var response = await admin.PostAsJsonAsync(TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body("IT", "Sicilia"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A name, country or rate the rules refuse answers 400 and stores nothing.</summary>
    /// <param name="name">The name to send.</param>
    /// <param name="country">The country to send.</param>
    /// <param name="ratePercent">The rate to send.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("", "DE", 19)]
    [InlineData("Valid", "", 19)]
    [InlineData("Valid", "DEU", 19)]
    [InlineData("Valid", "D1", 19)]
    [InlineData("Valid", "DE", -0.5)]
    [InlineData("Valid", "DE", 100.5)]
    [InlineData("Valid", "DE", 19.0005)]
    public async Task Create_RefusesAnInvalidField(string name, string country, double ratePercent)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body(country, null, (decimal)ratePercent, name: name));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAClient()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "create-tax-rule-forbidden");

        var response = await client.PostAsJsonAsync(TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
