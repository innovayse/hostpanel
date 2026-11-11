using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/client/{clientId}</c> against Postgres: the staff-facing, paginated list
/// of one client's own invoices.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists a client's invoices and finds the one just created for them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientIncludesTheClientsOwnInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 7m);

        var response = await admin.GetAsync($"/api/billing/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(invoiceId);
    }

    /// <summary>A client with no invoices lists an empty page, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientWithNoInvoicesReturnsAnEmptyPage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>Listing a client's invoices is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
