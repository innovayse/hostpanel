using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/bank-transfer/reject</c> against Postgres: rejecting returns the invoice to Unpaid so the client
/// can try again, tells the client why, and can never turn back a transfer that was already confirmed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RejectBankTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads an invoice with its payments from the database.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice.</returns>
    private async Task<Invoice> LoadAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>
    /// Rejecting with a note returns the invoice to Unpaid, clears the transfer marker, records no payment, mails the client the
    /// reason, and lets the client choose the transfer again.
    /// </summary>
    [Fact]
    public async Task Reject_ReturnsTheInvoiceToUnpaidTellsTheClientAndLetsThemTryAgain()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "bt-reject");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 100m);

        HttpResponseMessage? response = null;
        await BankTransferTestSeeds.RunTrackedAsync(host, async () =>
            response = await admin.PostAsJsonAsync(
                $"/api/billing/{invoiceId}/bank-transfer/reject", new { note = "Amount received was too low." }));

        response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var invoice = await LoadAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.Transactions.Should().BeEmpty();
        var mail = sender.Sent.Where(m => m.To == email && m.Subject.Contains("was not accepted")).Should().ContainSingle().Which;
        mail.Body.Should().Contain("Amount received was too low.").And.Contain($"INV-{invoiceId}");

        var again = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.PaymentPending);
    }

    /// <summary>Without a pending transfer there is nothing to reject: 409.</summary>
    [Fact]
    public async Task Reject_WhenNoTransferWaits_Returns409()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
    }

    /// <summary>A late reject cannot turn back a confirmed payment: 409, the invoice stays Paid.</summary>
    [Fact]
    public async Task Reject_AfterConfirm_Returns409AndKeepsTheInvoicePaid()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-late-reject");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 25m);
        (await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note = "Too late." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>The note is required and bounded: a missing or oversize one is a 400 and the transfer keeps waiting.</summary>
    /// <param name="note">The note under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithoutANote_Returns400AndKeepsWaiting(string note)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-note");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 25m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note });
        var tooLong = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note = new string('x', 1001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.PaymentPending);
    }

    /// <summary>An id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Reject_AnUnknownInvoice_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/bank-transfer/reject", new { note = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Staff only: anonymous callers get 401, and a client account gets 403.</summary>
    [Fact]
    public async Task Reject_IsForStaffOnly()
    {
        var anonymous = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/bank-transfer/reject", new { note = "No." });
        var (client, _, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-reject-client");

        var asClient = await client.PostAsJsonAsync("/api/billing/1/bank-transfer/reject", new { note = "No." });

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
