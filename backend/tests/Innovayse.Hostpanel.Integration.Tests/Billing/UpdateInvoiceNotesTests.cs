using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/billing/{id}/notes</c> against Postgres: writing and clearing an invoice's notes,
/// and the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateInvoiceNotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Writing notes stores them; writing <see langword="null"/> afterwards clears them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotesWritesThenClears()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var written = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/notes", new { notes = "Call before renewing" });
        written.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        fetchedBody.RootElement.GetProperty("notes").GetString().Should().Be("Call before renewing");

        var cleared = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/notes", new { notes = (string?)null });
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var reFetchedBody = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        reFetchedBody.RootElement.GetProperty("notes").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Updating notes on an id that names no invoice answers 404, not 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotesOnUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/notes", new { notes = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Updating notes is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotesRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/billing/1/notes", new { notes = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
