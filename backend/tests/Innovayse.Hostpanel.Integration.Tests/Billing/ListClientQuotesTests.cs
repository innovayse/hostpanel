using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/quotes/client/{clientId}</c> against Postgres: the staff-facing, paginated
/// list of one client's own quotes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientQuotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists a client's quotes and finds the one just created for them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientIncludesTheClientsOwnQuote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 60m);

        var response = await admin.GetAsync($"/api/billing/quotes/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(quoteId);
    }

    /// <summary>A client with no quotes lists an empty page, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientWithNoQuotesReturnsAnEmptyPage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>Listing a client's quotes is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/quotes/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
