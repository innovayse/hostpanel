using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>Integration tests for the <c>UpdateTaxRule</c> use case: <c>PUT /api/admin/tax-rules/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateTaxRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces every field and the rule reads back as sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesTheFields()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await TaxRuleTestSeeds.CreateAsync(admin, "ES");

        var response = await admin.PutAsJsonAsync(
            $"{TaxRuleTestSeeds.Route}/{id}", TaxRuleTestSeeds.Body("pt", "Madeira", 22m, isEnabled: false, name: "Madeira VAT"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rule = await TaxRuleTestSeeds.GetAsync(admin, id);
        rule.GetProperty("name").GetString().Should().Be("Madeira VAT");
        rule.GetProperty("country").GetString().Should().Be("PT");
        rule.GetProperty("region").GetString().Should().Be("Madeira");
        rule.GetProperty("ratePercent").GetDecimal().Should().Be(22m);
        rule.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
    }

    /// <summary>Saving a rule without moving it is not a clash with itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_KeepingThePlace_IsNotAClash()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await TaxRuleTestSeeds.CreateAsync(admin, "NL");

        var response = await admin.PutAsJsonAsync($"{TaxRuleTestSeeds.Route}/{id}", TaxRuleTestSeeds.Body("NL", null, 21m));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Moving a rule onto a place another rule holds is refused with 409 and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_OntoAnotherRulesPlace_RefusesAsAlreadyExisting()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "BE");
        var id = await TaxRuleTestSeeds.CreateAsync(admin, "LU");

        var response = await admin.PutAsJsonAsync($"{TaxRuleTestSeeds.Route}/{id}", TaxRuleTestSeeds.Body("BE"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TAX_RULE_ALREADY_EXISTS);
        (await TaxRuleTestSeeds.GetAsync(admin, id)).GetProperty("country").GetString().Should().Be("LU");
    }

    /// <summary>An id that names no rule answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"{TaxRuleTestSeeds.Route}/2147483000", TaxRuleTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{TaxRuleTestSeeds.Route}/1", TaxRuleTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
