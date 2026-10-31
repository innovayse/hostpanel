using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/billing/quotes/{id}</c> against Postgres: editing a quote's subject, stage and line
/// items, and the <c>QUOTE_NOT_FOUND</c> refusal for an id that names no quote.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateQuoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Editing a quote's subject and stage writes the new values.</summary>
    [Fact]
    public async Task UpdateWritesTheNewSubjectAndStage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 500m);

        var response = await admin.PutAsJsonAsync($"/api/billing/quotes/{quoteId}", new
        {
            subject = "Revised proposal",
            stage = "Delivered",
            validUntil = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            proposalText = (string?)null,
            customerNotes = (string?)null,
            adminNotes = (string?)null,
            items = new[] { new { id = (int?)null, description = "Design", unitPrice = 500m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/quotes/{quoteId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("subject").GetString().Should().Be("Revised proposal");
        body.RootElement.GetProperty("stage").GetString().Should().Be("Delivered");
    }

    /// <summary>Editing a line's values writes the new total, the discounted sum rounded once to the currency.</summary>
    [Fact]
    public async Task UpdateWithChangedLineValuesWritesTheNewTotal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId,
            subject = "Totals",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[]
            {
                new { description = "A", unitPrice = 10m, quantity = 3, discountPercent = 33.33m },
                new { description = "B", unitPrice = 0.004m, quantity = 1, discountPercent = 0m },
            },
        });
        var quoteId = await created.Content.ReadFromJsonAsync<int>();
        using var before = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/quotes/{quoteId}"));
        var lineId = before.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync($"/api/billing/quotes/{quoteId}", new
        {
            subject = "Totals",
            stage = "Draft",
            validUntil = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            proposalText = (string?)null,
            customerNotes = (string?)null,
            adminNotes = (string?)null,
            items = new[] { new { id = (int?)lineId, description = "A", unitPrice = 5m, quantity = 2, discountPercent = 10m, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var after = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/quotes/{quoteId}"));
        after.RootElement.GetProperty("total").GetDecimal().Should().Be(9.00m);
    }

    /// <summary>Updating an id that names no quote answers 404, not 400.</summary>
    [Fact]
    public async Task UpdateUnknownQuoteRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/billing/quotes/{BillingTestSeeds.NoSuchId}", new
        {
            subject = "Anything",
            stage = "Draft",
            validUntil = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            proposalText = (string?)null,
            customerNotes = (string?)null,
            adminNotes = (string?)null,
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.QUOTE_NOT_FOUND);
    }

    /// <summary>Updating a quote is not for anonymous callers.</summary>
    [Fact]
    public async Task UpdateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/billing/quotes/1", new
        {
            subject = "Anything",
            stage = "Draft",
            validUntil = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            proposalText = (string?)null,
            customerNotes = (string?)null,
            adminNotes = (string?)null,
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
