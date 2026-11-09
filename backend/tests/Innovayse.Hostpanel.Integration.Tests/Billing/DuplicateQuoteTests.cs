using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/quotes/{id}/duplicate</c> against Postgres: duplicating a quote as a new
/// draft, and the <c>QUOTE_NOT_FOUND</c> refusal for an id that names no quote.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DuplicateQuoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Duplicating a quote creates a new draft with a different id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateCreatesANewDraft()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 500m);

        var response = await admin.PostAsync($"/api/billing/quotes/{quoteId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var newId = await response.Content.ReadFromJsonAsync<int>();
        newId.Should().NotBe(quoteId);
    }

    /// <summary>Duplicating an id that names no quote answers 404 with <c>QUOTE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateUnknownQuoteRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/quotes/{BillingTestSeeds.NoSuchId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.QUOTE_NOT_FOUND);
    }

    /// <summary>Duplicating a quote is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/quotes/1/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
