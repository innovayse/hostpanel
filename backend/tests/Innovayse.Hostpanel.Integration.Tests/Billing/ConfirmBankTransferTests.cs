using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/bank-transfer/confirm</c> against Postgres: confirming goes through the one settlement path, so
/// the invoice turns Paid with its payment, its client ledger row, the payment mail and the fulfilment of its order; and the
/// guards that make a second confirm, a confirm of nothing and a confirm beside an unfinished card charge harmless.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConfirmBankTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads an invoice with its payments from the database.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice as stored.</returns>
    private async Task<Invoice> LoadAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Counts the client ledger rows written for one reference.</summary>
    /// <param name="reference">The reference the transfer is recorded under.</param>
    /// <returns>The number of ledger rows.</returns>
    private async Task<int> LedgerRowsAsync(string reference)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference);
    }

    /// <summary>
    /// Confirming pays the invoice through the shared path: Paid, one payment in the invoice's history under the reference,
    /// one client ledger row, the payment event's mail and the confirmation mail, and the linked order is fulfilled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_PaysTheInvoiceThroughTheSharedPath()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "bt-confirm");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 100m);
        var orderId = await BankTransferTestSeeds.LinkOrderAsync(host, invoiceId, clientId);
        var reference = $"INV-{invoiceId}";

        HttpResponseMessage? response = null;
        await BankTransferTestSeeds.RunTrackedAsync(host, async () =>
            response = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null));

        response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var invoice = await LoadAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.GatewayTransactionId.Should().Be(reference);
        invoice.Transactions.Should().ContainSingle(t =>
            t.Gateway == PaymentMethodNames.BankTransfer && t.TransactionId == reference && t.Amount == 100m);
        (await LedgerRowsAsync(reference)).Should().Be(1);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var row = await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == reference);
            row.Source.Should().Be(TransactionSource.Manual);
            row.InvoiceId.Should().Be(invoiceId);
            (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Active);
        }

        // PaymentReceivedEvent reached its handler (the payment receipt), and the transfer's own confirmation went out.
        sender.Sent.Where(m => m.To == email && m.Subject == "Payment Received").Should().ContainSingle();
        sender.Sent.Where(m => m.To == email && m.Body.Contains(reference) && m.Subject.Contains("was confirmed")).Should().ContainSingle();
    }

    /// <summary>A second confirm of the same transfer is refused with 409 and records no second payment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_Twice_RecordsOnePaymentAndRefusesTheSecond()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-double");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 60m);

        var first = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);
        var second = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(second)).Should().Be(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        (await LoadAsync(invoiceId)).Transactions.Should().HaveCount(1);
        (await LedgerRowsAsync($"INV-{invoiceId}")).Should().Be(1);
    }

    /// <summary>Two confirms racing each other never both pay: one wins with 204, the other is refused with 409.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_RacedByTwoAdmins_PaysExactlyOnce()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-race");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 75m);
        var otherAdmin = await BillingTestSeeds.AdminClientAsync(factory);

        var results = await Task.WhenAll(
            admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null),
            otherAdmin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null));

        results.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
        (await LoadAsync(invoiceId)).Transactions.Should().HaveCount(1);
        (await LedgerRowsAsync($"INV-{invoiceId}")).Should().Be(1);
    }

    /// <summary>An invoice nobody chose a transfer for has nothing to confirm: 409, still Unpaid, nothing recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_WhenNoTransferWaits_Returns409()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        var invoice = await LoadAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.Transactions.Should().BeEmpty();
    }

    /// <summary>A stored card charge that has not finished refuses the confirm, and the transfer keeps waiting.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_WhileAStoredChargeIsUnfinished_Returns409AndKeepsWaiting()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-confirm-claim");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 30m);
        await BillingTestSeeds.SeedUnresolvedClaimAsync(factory, invoiceId, clientId, pending: true);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        var invoice = await LoadAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.PaymentPending);
        invoice.Transactions.Should().BeEmpty();
    }

    /// <summary>A claim held for an operator does not stop the confirm: recording the payment is how the operator settles it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_OnAHeldClaim_IsAllowed()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-confirm-held");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 30m);
        await BillingTestSeeds.SeedHeldClaimAsync(factory, invoiceId, clientId);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>An id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_AnUnknownInvoice_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/bank-transfer/confirm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Staff only: anonymous callers get 401, and a client account gets 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_IsForStaffOnly()
    {
        var anonymous = await factory.CreateClient().PostAsync("/api/billing/1/bank-transfer/confirm", content: null);
        var (client, _, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-confirm-client");

        var asClient = await client.PostAsync("/api/billing/1/bank-transfer/confirm", content: null);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Confirm_WithNonPositiveId_Returns400ValidationError()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/billing/0/bank-transfer/confirm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
