using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/bank-transfer</c> against Postgres: the client moves their own invoice to
/// <c>PaymentPending</c> as a manual bank transfer and gets the bank details and a reference built from the invoice number,
/// without the internal client id; and every refusal that keeps the money from being taken twice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SelectMyBankTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the stored status of an invoice from the database.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>Its status and whether the transfer marker is set.</returns>
    private async Task<(InvoiceStatus Status, bool Marked)> StoredAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        return (invoice.Status, invoice.BankTransferRequestedAt is not null);
    }

    /// <summary>
    /// Choosing the transfer answers the bank details and the reference, moves the invoice to PaymentPending, marks it as a
    /// transfer and never shows the client id.
    /// </summary>
    [Fact]
    public async Task Select_TheOwnersInvoice_AnswersTheDetailsAndMovesToPaymentPending()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-select");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("reference").GetString().Should().Be($"INV-{invoiceId}");
        body.GetProperty("iban").GetString().Should().Be(BankTransferTestSeeds.Iban);
        body.GetProperty("swift").GetString().Should().Be("EXAMPAMX");
        body.GetProperty("accountName").GetString().Should().Be("Example Hosting LLC");
        body.GetProperty("bankName").GetString().Should().Be("Example Bank");
        body.GetProperty("instructions").GetString().Should().Be("Pay within five days.");
        body.GetProperty("amountDue").GetDecimal().Should().Be(100m);
        body.EnumerateObject().Select(p => p.Name).Should().NotContain(name => name.Contains("client", StringComparison.OrdinalIgnoreCase));
        (await StoredAsync(invoiceId)).Should().Be((InvoiceStatus.PaymentPending, true));
    }

    /// <summary>Choosing it again answers the same details and changes nothing, so a double click is harmless.</summary>
    [Fact]
    public async Task Select_TwiceInARow_AnswersTheSameDetailsBothTimes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-twice");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 40m);

        var first = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);
        var second = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetString().Should().Be($"INV-{invoiceId}");
        (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.PaymentPending);
    }

    /// <summary>Another client's invoice answers like one that does not exist, and is left untouched.</summary>
    [Fact]
    public async Task Select_AStrangersInvoice_Returns404AndChangesNothing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (_, ownerClientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-owner");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "bt-stranger");

        var response = await stranger.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>With the switch off the client cannot choose it: 409 <c>BANK_TRANSFER_NOT_AVAILABLE</c>, invoice untouched.</summary>
    [Fact]
    public async Task Select_WhenTheAdminSwitchedItOff_Returns409()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.DisableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-off");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.BANK_TRANSFER_NOT_AVAILABLE);
        (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// A transfer switched on with every bank detail empty cannot be chosen: 409 <c>BANK_TRANSFER_NOT_AVAILABLE</c>, invoice
    /// untouched, so the payer is never left with a reference and nowhere to send the money.
    /// </summary>
    [Fact]
    public async Task Select_WhenTheAdminSetNoBankDetails_Returns409()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-empty");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        try
        {
            await BankTransferTestSeeds.EnableWithoutDetailsAsync(admin);

            var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.BANK_TRANSFER_NOT_AVAILABLE);
            (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
        }
        finally
        {
            await BankTransferTestSeeds.EnableAsync(admin);
        }
    }

    /// <summary>A paid invoice is not open for payment: 400 <c>INVOICE_NOT_PAYABLE</c>.</summary>
    [Fact]
    public async Task Select_APaidInvoice_Returns400NotPayable()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-paid");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
    }

    /// <summary>A stored card charge that has not finished blocks the transfer, as it blocks a hosted payment.</summary>
    [Fact]
    public async Task Select_WhileAStoredChargeIsUnfinished_Returns409AndKeepsTheInvoiceOpen()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-claim");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.SeedUnresolvedClaimAsync(factory, invoiceId, clientId, pending: true);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A charge held for an operator blocks the transfer too, so the payer is never asked to pay again while money is held.</summary>
    [Fact]
    public async Task Select_WhileAStoredChargeIsHeld_Returns409Held()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-held");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.SeedHeldClaimAsync(factory, invoiceId, clientId);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_STORED_CHARGE_HELD);
    }

    /// <summary>A hosted payment the payer may still be completing blocks the transfer: 409 <c>GATEWAY_SESSION_ALREADY_STARTED</c>.</summary>
    [Fact]
    public async Task Select_WhileAHostedPaymentIsOpen_Returns409()
    {
        var plugin = new FakePaymentPlugin { CreatePaymentResult = new("gw-bt", "https://pg.example/pay?mdOrder=gw-bt") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "bt-session");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = BillingTestSeeds.AllowedReturnUrl }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_SESSION_ALREADY_STARTED);
        (await StoredAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// Choosing the transfer mails the client the bank details and staff a notice, once each, and a repeat click mails nothing
    /// more.
    /// </summary>
    [Fact]
    public async Task Select_MailsTheClientTheDetailsAndStaffANoticeOnce()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "bt-mail");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        await BankTransferTestSeeds.RunTrackedAsync(host, async () =>
        {
            (await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);
        });

        var toClient = sender.Sent.Where(m => m.To == email && m.Subject.Contains($"#{invoiceId}") && m.Body.Contains(BankTransferTestSeeds.Iban)).ToList();
        toClient.Should().ContainSingle();
        toClient[0].Body.Should().Contain($"INV-{invoiceId}").And.Contain("EXAMPAMX").And.Contain("Example Hosting LLC");
        var toStaff = sender.Sent.Where(m => m.To == BankTransferTestSeeds.StaffEmail && m.Subject.Contains($"#{invoiceId}")).ToList();
        toStaff.Should().ContainSingle();
        toStaff[0].Body.Should().Contain($"INV-{invoiceId}");
    }

    /// <summary>The action is not for anonymous callers.</summary>
    [Fact]
    public async Task SelectRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/me/invoices/1/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    [Fact]
    public async Task Select_WithNonPositiveId_Returns400ValidationError()
    {
        var (client, _, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-validation");

        var response = await client.PostAsync("/api/me/invoices/0/bank-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
