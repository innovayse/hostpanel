using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/quotes/{id}</c> against Postgres: reading a single quote, and the
/// <c>QUOTE_NOT_FOUND</c> refusal for an id that names no quote.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetQuoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads back the quote they just created, named after its client.</summary>
    [Fact]
    public async Task GetByIdReturnsTheCreatedQuote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 250m);

        var response = await admin.GetAsync($"/api/billing/quotes/{quoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>A discounted line that leaves more decimals than the currency allows reads back rounded, line and total alike.</summary>
    [Fact]
    public async Task GetByIdReturnsTheDiscountedAmountsRoundedToTheCurrency()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId,
            subject = "Rounded",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[] { new { description = "Hosting", unitPrice = 10m, quantity = 3, discountPercent = 33.33m } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var quoteId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.GetAsync($"/api/billing/quotes/{quoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(20.00m);
        body.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal().Should().Be(20.00m);
    }

    /// <summary>Reading an id that names no quote answers 404 with the <c>QUOTE_NOT_FOUND</c> code.</summary>
    [Fact]
    public async Task GetUnknownQuoteRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.QUOTE_NOT_FOUND);
    }

    /// <summary>Reading a quote is not for anonymous callers.</summary>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/quotes/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
