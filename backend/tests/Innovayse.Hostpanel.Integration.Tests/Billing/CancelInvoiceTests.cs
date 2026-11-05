using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/cancel</c> against Postgres: cancelling an invoice, the refusal to
/// cancel a paid one, and the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Cancelling an unpaid invoice transitions it to Cancelled.</summary>
    [Fact]
    public async Task CancelTransitionsAnUnpaidInvoiceToCancelled()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Cancelled));
    }

    /// <summary>
    /// An invoice with a stored card charge waiting for the payer or the gateway is not cancelled: the charge could still
    /// take money, so the refusal is a 409 with <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> and the invoice stays open.
    /// </summary>
    /// <param name="pending"><see langword="true"/> for a pending charge, <see langword="false"/> for an action required one.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellingAnInvoiceWithAnUnfinishedStoredChargeRefusesConflict(bool pending)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.SeedUnresolvedClaimAsync(factory, invoiceId, clientId, pending);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().NotBe(nameof(InvoiceStatus.Cancelled));
    }

    /// <summary>
    /// An invoice with a charge held for an operator can be cancelled: cancelling is one way the operator settles it.
    /// </summary>
    [Fact]
    public async Task CancellingAnInvoiceWithAHeldStoredChargeIsAllowed()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.SeedHeldClaimAsync(factory, invoiceId, clientId);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Cancelled));
    }

    /// <summary>Cancelling a paid invoice is refused with a 400.</summary>
    [Fact]
    public async Task CancellingAPaidInvoiceRefusesBadRequest()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Cancelling an invoice returns the credit applied to it to the client's balance.</summary>
    [Fact]
    public async Task CancellingReturnsAppliedCredit()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 4m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 4m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(4m);
    }

    /// <summary>Cancelling an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task CancelUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Cancelling an invoice is not for anonymous callers.</summary>
    [Fact]
    public async Task CancelRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/1/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Cancelling an invoice that already took part of its total is refused with <c>INVOICE_HAS_PAYMENTS</c> and stays open.</summary>
    [Fact]
    public async Task CancellingAPartPaidInvoiceRefusesWithHasPayments()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 4m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_HAS_PAYMENTS);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }
}
