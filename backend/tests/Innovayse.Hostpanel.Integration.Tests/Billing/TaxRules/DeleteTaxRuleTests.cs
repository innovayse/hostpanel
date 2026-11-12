using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>Integration tests for the <c>DeleteTaxRule</c> use case: <c>DELETE /api/admin/tax-rules/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteTaxRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a rule removes it: a later read of the id answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheRule()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await TaxRuleTestSeeds.CreateAsync(admin, "SE");

        var response = await admin.DeleteAsync($"{TaxRuleTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"{TaxRuleTestSeeds.Route}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An id that names no rule answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"{TaxRuleTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{TaxRuleTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
