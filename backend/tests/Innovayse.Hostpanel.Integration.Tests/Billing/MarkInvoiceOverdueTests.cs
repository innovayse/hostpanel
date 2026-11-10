using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/mark-overdue</c> against Postgres: the admin's manual transition to
/// Overdue, the refusal when the invoice is not Unpaid, and the <c>INVOICE_NOT_FOUND</c> refusal
/// for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MarkInvoiceOverdueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Marking an unpaid invoice overdue transitions it to Overdue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkOverdueTransitionsAnUnpaidInvoiceToOverdue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/mark-overdue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Overdue));
    }

    /// <summary>Marking a draft invoice overdue is refused with a 400: only Unpaid transitions.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkOverdueOnADraftInvoiceRefusesBadRequest()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m, isDraft: true);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/mark-overdue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Marking an id that names no invoice overdue answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkOverdueOnUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/mark-overdue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Marking an invoice overdue is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkOverdueRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/1/mark-overdue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
