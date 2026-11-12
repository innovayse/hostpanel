using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>Integration tests for the <c>GetTaxRule</c> use case: <c>GET /api/admin/tax-rules/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTaxRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A stored rule reads back with every field.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ReturnsTheRule()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await TaxRuleTestSeeds.CreateAsync(admin, "AT", null, 20m);

        var rule = await TaxRuleTestSeeds.GetAsync(admin, id);

        rule.GetProperty("id").GetInt32().Should().Be(id);
        rule.GetProperty("country").GetString().Should().Be("AT");
        rule.GetProperty("region").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        rule.GetProperty("ratePercent").GetDecimal().Should().Be(20m);
    }

    /// <summary>An id that names no rule answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{TaxRuleTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{TaxRuleTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
