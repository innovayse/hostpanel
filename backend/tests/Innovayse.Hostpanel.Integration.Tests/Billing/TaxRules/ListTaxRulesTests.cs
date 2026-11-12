using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>Integration tests for the <c>ListTaxRules</c> use case: <c>GET /api/admin/tax-rules</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTaxRulesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list holds every rule, each country's country-wide rule before its regions.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsEveryRuleCountryWideFirst()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var region = await TaxRuleTestSeeds.CreateAsync(admin, "CZ", "Praha");
        var wide = await TaxRuleTestSeeds.CreateAsync(admin, "CZ");

        var rules = await admin.GetFromJsonAsync<JsonElement>(TaxRuleTestSeeds.Route);

        var ids = rules.EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain([wide, region]);
        ids.IndexOf(wide).Should().BeLessThan(ids.IndexOf(region));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(TaxRuleTestSeeds.Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
