using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/quotes</c> against Postgres: the admin-wide, paginated quote list.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListQuotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A quote just created appears in the admin's list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedQuote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 90m);

        var response = await admin.GetAsync("/api/billing/quotes?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(quoteId);
        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == quoteId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>A quote is priced in its client's currency, and the list and the detail both say so.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteCarriesTheClientsCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQM", "997", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, "ZQM");
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 90m);

        var list = await admin.GetAsync("/api/billing/quotes?page=1&pageSize=100");
        var detail = await admin.GetAsync($"/api/billing/quotes/{quoteId}");

        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == quoteId)
            .GetProperty("currency").GetString().Should().Be("ZQM");
        using var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailBody.RootElement.GetProperty("currency").GetString().Should().Be("ZQM");
    }

    /// <summary>Listing quotes is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/quotes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
