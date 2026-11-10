using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/duplicate</c> against Postgres: duplicating an invoice creates a new
/// draft carrying the same line items, and the <c>INVOICE_NOT_FOUND</c> refusal for an id that
/// names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DuplicateInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Duplicating an invoice creates a new draft carrying the same line items.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateCreatesADraftCopy()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            items = new[] { new { description = "Hosting", unitPrice = 5m, quantity = 2 } },
        });
        var invoiceId = await created.Content.ReadFromJsonAsync<int>();

        var duplicated = await admin.PostAsync($"/api/billing/{invoiceId}/duplicate", content: null);

        duplicated.StatusCode.Should().Be(HttpStatusCode.Created);
        var newId = await duplicated.Content.ReadFromJsonAsync<int>();
        newId.Should().NotBe(invoiceId);
    }

    /// <summary>Duplicating an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Duplicating an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/1/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
