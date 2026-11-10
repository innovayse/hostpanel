using System.Net;
using System.Net.Http.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// What paying a credit top-up invoice does, against Postgres, through every road that can pay an invoice: the balance rises
/// exactly once, in the save that marks the invoice Paid, whatever replays, double completions or racing payments arrive;
/// the top-up fulfils nothing and is no revenue beyond the cash it took; credit cannot pay for it; and a refund or a
/// cancellation of it behaves as the owner decided.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreditTopUpPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The gateway module the fake plugin is registered under.</summary>
    private const string Module = "bank-gateway";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> GatewayHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>Builds a fake gateway that reports the top-up as paid under a fresh reference.</summary>
    /// <param name="amountMinor">What the gateway says it took, in minor units.</param>
    /// <param name="resolver">The resolver the plugin is registered in.</param>
    /// <returns>The plugin.</returns>
    private static FakePaymentPlugin PaidPlugin(long amountMinor, FakePaymentPluginResolver resolver)
    {
        var plugin = new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, $"ref-{Guid.NewGuid():N}", "orderStatus:2", AmountMinor: amountMinor),
            CreatePaymentResult = new($"gw-{Guid.NewGuid():N}", "https://pg.example/pay"),
        };
        resolver.Register(Module, plugin);
        return plugin;
    }

    /// <summary>Starts a gateway session on the invoice as its owner.</summary>
    /// <param name="owner">The invoice owner's session.</param>
    /// <param name="invoiceId">The top-up invoice.</param>
    /// <returns>A task that completes once the session is open.</returns>
    private static async Task StartAsync(HttpClient owner, int invoiceId)
    {
        var started = await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });
        started.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Asks the gateway to complete the payment, as the payer's return page does.</summary>
    /// <param name="owner">The invoice owner's session.</param>
    /// <param name="invoiceId">The top-up invoice.</param>
    /// <returns>The HTTP response.</returns>
    private static Task<HttpResponseMessage> CompleteAsync(HttpClient owner, int invoiceId) =>
        owner.PostAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", content: null);

    /// <summary>Records a full manual payment on the invoice as the admin.</summary>
    /// <param name="admin">The admin session.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The payment.</param>
    /// <returns>The HTTP response.</returns>
    private static Task<HttpResponseMessage> PayAsync(HttpClient admin, int invoiceId, decimal amount) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"manual-{Guid.NewGuid():N}",
            amount,
            fees = 0m,
        });

    /// <summary>Asserts the invariant that holds after every scenario: the balance is exactly the sum of the credit history.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The history, oldest first.</returns>
    private static async Task<List<Transaction>> AssertHistoryAddsUpAsync(WebApplicationFactory<Program> host, int clientId)
    {
        var history = await CreditTestSeeds.HistoryAsync(host, clientId);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(history.Sum(t => t.CreditChange!.Value));
        history.Last().BalanceAfter.Should().Be(await BillingTestSeeds.CreditBalanceOfAsync(host, clientId));
        return history;
    }

    /// <summary>
    /// A confirmed bank transfer of a top-up pays the invoice and puts exactly its amount on the balance: one history line
    /// that is the payment's own ledger entry, the client mailed the new balance, and nothing fulfilled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BankTransfer_Confirm_CreditsTheBalanceOnce()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-bank");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        (await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage? confirmed = null;
        await BankTransferTestSeeds.RunTrackedAsync(host, async () =>
            confirmed = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null));

        confirmed!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(host, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(40m);
        var history = await AssertHistoryAddsUpAsync(host, clientId);
        var line = history.Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.TopUp);
        line.CreditChange.Should().Be(40m);
        line.BalanceAfter.Should().Be(40m);
        line.InvoiceId.Should().Be(invoiceId);

        // The payment's own ledger entry carries the movement: no second line for the same money.
        line.AmountIn.Should().Be(40m);
        line.PaymentMethod.Should().Be(PaymentMethodNames.BankTransfer);
        (await CreditTestSeeds.LedgerAsync(host, clientId)).Should().ContainSingle();

        sender.Sent.Where(m => m.To == email && m.Subject.StartsWith("Your credit balance is now")).Should().ContainSingle()
            .Which.Body.Should().Contain("40");
    }

    /// <summary>A top-up fulfils nothing: no fulfilment record, no order, no service, no domain is touched by paying it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PayingATopUp_FulfilsNothing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-nofulfil");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        (await PayAsync(admin, invoiceId, 40m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.FulfilmentRecords.AsNoTracking().CountAsync(r => r.InvoiceId == invoiceId)).Should().Be(0);
        (await context.Orders.AsNoTracking().CountAsync(o => o.ClientId == clientId)).Should().Be(0);
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId)).Should().Be(0);
        (await context.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId))
            .Items.Should().OnlyContain(i => i.ClientServiceId == null && i.RenewalPeriodStart == null && i.ProductSnapshot == null);
    }

    /// <summary>A second confirm of the same transfer is refused and credits nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BankTransfer_ConfirmTwice_CreditsOnce()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-replay");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 25m);
        await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        var first = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);
        var second = await admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(25m);
        (await AssertHistoryAddsUpAsync(factory, clientId)).Should().ContainSingle();
    }

    /// <summary>Two admins confirming the one transfer together: one wins, and the balance rises once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BankTransfer_ConfirmRacedByTwoAdmins_CreditsOnce()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var otherAdmin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-adminrace");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 75m);
        await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        var results = await Task.WhenAll(
            admin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null),
            otherAdmin.PostAsync($"/api/billing/{invoiceId}/bank-transfer/confirm", content: null));

        results.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(75m);
        (await AssertHistoryAddsUpAsync(factory, clientId)).Should().ContainSingle();
    }

    /// <summary>A rejected transfer credits nothing, and the invoice can still be paid and credited later.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BankTransfer_Reject_CreditsNothing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-reject");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 25m);
        await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);

        var rejected = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note = "Nothing arrived." });

        rejected.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await PayAsync(admin, invoiceId, 25m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(25m);
    }

    /// <summary>A payment staff record by hand completes the top-up and credits it once; a second full payment is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualPayment_CreditsOnce_AndAReplayIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-manual");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 60m);

        var first = await PayAsync(admin, invoiceId, 60m);
        var replay = await PayAsync(admin, invoiceId, 60m);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(replay)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(60m);
        (await AssertHistoryAddsUpAsync(factory, clientId)).Should().ContainSingle();
    }

    /// <summary>A part payment credits nothing; the payment that completes the top-up credits the whole amount, once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PartPayments_CreditTheWholeAmountWhenTheLastOneCompletesIt()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-parts");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 60m);

        (await PayAsync(admin, invoiceId, 20m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();

        (await PayAsync(admin, invoiceId, 40m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(60m);
        var line = (await AssertHistoryAddsUpAsync(factory, clientId)).Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.TopUp);
        line.CreditChange.Should().Be(60m);
    }

    /// <summary>A bulk mark-paid of a top-up credits it, like every other road that pays an invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BulkMarkPaid_CreditsOnce()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-bulk");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 30m);

        var marked = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = nameof(InvoiceBulkAction.MarkPaid),
        });
        var again = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = nameof(InvoiceBulkAction.MarkPaid),
        });

        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(30m);
        (await AssertHistoryAddsUpAsync(factory, clientId)).Should().ContainSingle();
    }

    /// <summary>A gateway that reports the top-up paid credits the balance once, with the payment's own ledger entry as the line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_Completion_CreditsTheBalanceOnce()
    {
        var resolver = new FakePaymentPluginResolver();
        var plugin = PaidPlugin(4000, resolver);
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-gateway");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        var completed = await CompleteAsync(owner, invoiceId);

        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(40m);
        var line = (await AssertHistoryAddsUpAsync(host, clientId)).Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.TopUp);
        line.Source.Should().Be(TransactionSource.GatewayPayment);
        line.TransactionId.Should().Be(plugin.StatusResult.TransactionId);
        (await CreditTestSeeds.LedgerAsync(host, clientId)).Should().ContainSingle();
    }

    /// <summary>Completing the same payment again, as a reloaded return page or the reconciler does, credits nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_CompletionReplayed_CreditsOnce()
    {
        var resolver = new FakePaymentPluginResolver();
        PaidPlugin(4000, resolver);
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-gwreplay");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        var first = await CompleteAsync(owner, invoiceId);
        var second = await CompleteAsync(owner, invoiceId);
        var third = await CompleteAsync(owner, invoiceId);

        new[] { first, second, third }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(40m);
        (await AssertHistoryAddsUpAsync(host, clientId)).Should().ContainSingle();
    }

    /// <summary>The return page and the reconciler completing at the same moment: both answer Paid, the balance rises once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_CompletionDoneTwiceAtOnce_CreditsOnce()
    {
        var resolver = new FakePaymentPluginResolver();
        PaidPlugin(4000, resolver);
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-gwdouble");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        var results = await Task.WhenAll(CompleteAsync(owner, invoiceId), CompleteAsync(owner, invoiceId), CompleteAsync(owner, invoiceId));

        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(40m);
        (await AssertHistoryAddsUpAsync(host, clientId)).Should().ContainSingle();
    }

    /// <summary>
    /// A gateway completion racing a payment staff record: the top-up is credited once whichever lands first, and the
    /// loser's money is never lost (a gateway payment that cannot pay the settled invoice goes to the balance as a surplus).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_CompletionRacingAManualPayment_CreditsTheTopUpOnce()
    {
        var resolver = new FakePaymentPluginResolver();
        PaidPlugin(4000, resolver);
        var host = GatewayHost(resolver);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-gwrace");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        await Task.WhenAll(CompleteAsync(owner, invoiceId), PayAsync(admin, invoiceId, 40m));

        (await CreditTestSeeds.InvoiceAsync(host, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        var history = await AssertHistoryAddsUpAsync(host, clientId);
        history.Count(t => t.CreditMovement == CreditMovementType.TopUp).Should().Be(1);
        history.Where(t => t.CreditMovement != CreditMovementType.TopUp).Should()
            .OnlyContain(t => t.CreditMovement == CreditMovementType.PaymentSurplus && t.CreditChange == 40m);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().BeOneOf(40m, 80m);
    }

    /// <summary>A gateway that took more than the top-up credits the top-up and the surplus as two lines, and nothing is lost.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_TookMoreThanDue_CreditsTheTopUpAndTheSurplus()
    {
        var resolver = new FakePaymentPluginResolver();
        PaidPlugin(5000, resolver);
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-surplus");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        (await CompleteAsync(owner, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(50m);
        var history = await AssertHistoryAddsUpAsync(host, clientId);
        history.Select(t => (t.CreditMovement, t.CreditChange)).Should().BeEquivalentTo(new (CreditMovementType?, decimal?)[]
        {
            (CreditMovementType.TopUp, 40m),
            (CreditMovementType.PaymentSurplus, 10m),
        });
    }

    /// <summary>A gateway that took less than the top-up is refused as a mismatch: nothing is credited and the invoice stays open.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_TookLessThanDue_CreditsNothing()
    {
        var resolver = new FakePaymentPluginResolver();
        PaidPlugin(1000, resolver);
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-short");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        var completed = await CompleteAsync(owner, invoiceId);

        completed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(completed)).Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
        (await CreditTestSeeds.InvoiceAsync(host, invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A gateway that has not taken the money yet credits nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Gateway_StillPending_CreditsNothing()
    {
        var resolver = new FakePaymentPluginResolver();
        var plugin = PaidPlugin(4000, resolver);
        plugin.StatusResult = new(GatewayPaymentState.Pending, null, "orderStatus:0");
        var host = GatewayHost(resolver);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "topup-pending");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await StartAsync(owner, invoiceId);

        (await CompleteAsync(owner, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
    }

    /// <summary>
    /// Two top-ups of one client paid at the same moment both reach the balance: the later save finds the balance moved,
    /// is refused, and its retry credits it on the fresh balance, so neither is lost and neither is counted twice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoTopUpsOfOneClientPaidTogether_AreBothCredited()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var otherAdmin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-two");
        var first = await CreditTestSeeds.TopUpAsync(owner, 40m);
        var second = await CreditTestSeeds.TopUpAsync(owner, 60m);

        var results = await Task.WhenAll(PayAsync(admin, first, 40m), PayAsync(otherAdmin, second, 60m));
        foreach (var (response, invoiceId, amount) in new[] { (results[0], first, 40m), (results[1], second, 60m) })
        {
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                (await PayAsync(admin, invoiceId, amount)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            }
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(100m);
        var history = await AssertHistoryAddsUpAsync(factory, clientId);
        history.Select(t => t.InvoiceId).Should().BeEquivalentTo(new int?[] { first, second });
        history.Select(t => t.CreditChange).Should().BeEquivalentTo(new decimal?[] { 40m, 60m });
    }

    /// <summary>Credit cannot pay for credit: staff and the client are both refused, and nothing moves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreditCannotBeAppliedToATopUp()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-nocredit");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 50m);
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        var byStaff = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 10m });
        var byClient = await owner.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/credit", new { amount = 10m });

        byStaff.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(byStaff)).Should().Be(HostpanelErrorCodes.INVOICE_TOP_UP_NO_CREDIT);
        byClient.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(byClient)).Should().Be(HostpanelErrorCodes.INVOICE_TOP_UP_NO_CREDIT);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(50m);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Credit.Should().Be(0m);
    }

    /// <summary>The line cannot be edited into another invoice, and the invoice cannot be duplicated.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ATopUpCannotBeEditedOrDuplicated()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-fixed");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        var itemId = (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Items[0].Id;

        var edit = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)itemId, description = "Hosting", unitPrice = 400m, quantity = 1, isDeleted = false } },
        });
        var duplicate = await admin.PostAsync($"/api/billing/{invoiceId}/duplicate", content: null);

        edit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(edit)).Should().Be(HostpanelErrorCodes.INVOICE_TOP_UP_FIXED);
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Total.Should().Be(40m);
    }

    /// <summary>An unpaid top-up past its due date is never a debt: the overdue sweep leaves it alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheOverdueSweepLeavesATopUpAlone()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-overdue");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        var backDated = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow.AddDays(-30),
            dueDate = DateTimeOffset.UtcNow.AddDays(-1),
            paymentMethod = (string?)null,
            taxRate = 0m,
        });
        backDated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>().InvokeAsync(
                new Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron.CheckOverdueInvoicesCronCommand(),
                CancellationToken.None);
        }

        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// The cash is counted once. Paying the top-up is income of the amount paid; spending that credit on another invoice
    /// adds none, because an invoice paid with credit counts only what is left after the credit.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheMoneyIsNotCountedAsRevenueTwice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-income");
        var topUp = await CreditTestSeeds.TopUpAsync(owner, 40m);
        (await PayAsync(admin, topUp, 40m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var spent = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 25m);

        var applied = await owner.PostAsJsonAsync($"/api/me/invoices/{spent}/credit", new { amount = 25m });

        applied.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, spent)).Status.Should().Be(InvoiceStatus.Paid);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(15m);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var income = await context.IncomeEntries().Where(e => e.ClientId == clientId).ToListAsync();
        income.Sum(e => e.Amount).Should().Be(40m);
    }

    /// <summary>A top-up nobody paid can be cancelled; nothing was credited, so nothing moves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellingAnUnpaidTopUp_MovesNoCredit()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-cancel");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        var cancelled = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Cancelled);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
    }

    /// <summary>A paid top-up cannot be cancelled: its credit would stay on the balance with the invoice gone. A refund is the way back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APaidTopUpCannotBeCancelled()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-cancelpaid");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await PayAsync(admin, invoiceId, 40m);

        var cancelled = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(40m);
    }

    /// <summary>
    /// A manual refund of a paid top-up takes the refunded amount off the balance in the same save, as one history line that is
    /// the refund's own ledger entry; the invoice turns Refunded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualRefund_TakesTheCreditOffTheBalance()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-refund");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await PayAsync(admin, invoiceId, 40m);

        var refunded = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 40m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });

        refunded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Refunded);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var history = await AssertHistoryAddsUpAsync(factory, clientId);
        history.Select(t => (t.CreditMovement, t.CreditChange, t.BalanceAfter)).Should().BeEquivalentTo(new (CreditMovementType?, decimal?, decimal?)[]
        {
            (CreditMovementType.TopUp, 40m, 40m),
            (CreditMovementType.TopUpRefunded, -40m, 0m),
        });
        history[1].AmountOut.Should().Be(40m);
    }

    /// <summary>A client who already spent the credit leaves the balance short: the refund is refused whole and nothing is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refund_OfCreditAlreadySpent_IsRefusedAndRecordsNothing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-spent");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await PayAsync(admin, invoiceId, 40m);
        var spent = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 30m);
        (await owner.PostAsJsonAsync($"/api/me/invoices/{spent}/credit", new { amount = 30m })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refund = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 40m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });

        refund.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(refund)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Transactions.Should().OnlyContain(t => t.Type == InvoiceTransactionType.Payment);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
        (await CreditTestSeeds.LedgerAsync(factory, clientId)).Should().NotContain(t => t.AmountOut > 0 && t.InvoiceId == invoiceId);

        // What the balance still holds can be refunded: the refund of 10 takes the last of it.
        var partial = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });
        partial.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        await AssertHistoryAddsUpAsync(factory, clientId);
    }

    /// <summary>A top-up is refunded by hand only: a gateway refund or a refund back to the balance would send the same money twice.</summary>
    /// <param name="refundType">A refund type other than Manual.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(nameof(RefundType.Gateway))]
    [InlineData(nameof(RefundType.CreditBalance))]
    public async Task Refund_OtherThanManual_IsRefused(string refundType)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, $"topup-refundtype-{refundType}");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await PayAsync(admin, invoiceId, 40m);

        var refund = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new { amount = 40m, refundType });

        refund.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(refund)).Should().Be(HostpanelErrorCodes.INVOICE_TOP_UP_REFUND_MANUAL_ONLY);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(40m);
    }

    /// <summary>The payment entry of a top-up is part of the credit history: it cannot be edited or deleted, so the balance cannot drift from it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ThePaymentEntry_CannotBeEditedOrDeleted()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-locked");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);
        await PayAsync(admin, invoiceId, 40m);
        var entry = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Single();

        var deleted = await admin.DeleteAsync($"/api/transactions/{entry.Id}");
        var edited = await admin.PutAsJsonAsync($"/api/transactions/{entry.Id}", new
        {
            date = entry.Date,
            description = "Changed",
            transactionId = entry.TransactionId,
            invoiceId = entry.InvoiceId,
            paymentMethod = entry.PaymentMethod,
            amountIn = 99m,
            amountOut = 0m,
            fees = 0m,
        });

        deleted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(deleted)).Should().Be(HostpanelErrorCodes.TRANSACTION_CREDIT_LOCKED);
        edited.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(edited)).Should().Be(HostpanelErrorCodes.TRANSACTION_CREDIT_LOCKED);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Single().AmountIn.Should().Be(40m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(40m);
    }
}
