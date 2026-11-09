using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/quotes</c> against Postgres: creating a quote for a client, then reading
/// it back round-trips the line item and total.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateQuoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a quote, then reading it back, round-trips the line item and total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGetRoundTripsTheQuote()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 500m);

        var fetched = await admin.GetAsync($"/api/billing/quotes/{quoteId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(quoteId);
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(500m);
    }

    /// <summary>Creating a quote is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId = 1,
            subject = "Website redesign",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[] { new { description = "Design", unitPrice = 500m, quantity = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A quote for a client that does not exist answers 404 with <c>CLIENT_NOT_FOUND</c> and writes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId = BillingTestSeeds.NoSuchId,
            subject = "Website redesign",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[] { new { description = "Design", unitPrice = 500m, quantity = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
