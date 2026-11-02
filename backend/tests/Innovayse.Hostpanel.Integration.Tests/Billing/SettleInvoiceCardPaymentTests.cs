using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="SettleInvoiceCardPaymentCommand"/> through the real <see cref="IMessageBus"/>: the
/// settlement of an invoice by a card payment another module has already verified, the replay of
/// that same payment, and the refund of a payment the invoice cannot record.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SettleInvoiceCardPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The amount every seeded invoice is issued for, in USD.</summary>
    private const decimal InvoiceTotal = 20m;

    /// <summary>The fake card gateway every refund in this class goes to.</summary>
    private readonly FakeCardPaymentGateway _cardGateway = new() { IsConfigured = true };

    /// <summary>The host whose card gateway is <see cref="_cardGateway"/>, built on first use and shared by the test.</summary>
    private WebApplicationFactory<Program>? _host;

    /// <summary>The invoice is marked paid and the transaction is written to the ledger.</summary>
    [Fact]
    public async Task HandleAsync_UnpaidInvoice_SettlesItAndWritesTheLedgerRowAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(Payment(invoiceId, transactionId));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId)).Source
            .Should().Be(TransactionSource.GatewayPayment);
        _cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>
    /// Recording a charge already taken needs no gateway: with no card gateway key a payable invoice
    /// still settles, with its ledger row, and nothing is refunded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CardGatewayNotConfigured_StillSettlesAPayableInvoiceAsync()
    {
        _cardGateway.IsConfigured = false;
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(Payment(invoiceId, transactionId));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await context.Transactions.AsNoTracking().AnyAsync(t => t.TransactionId == transactionId)).Should().BeTrue();
        _cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>
    /// A settlement that has to be refused with no card gateway key does not call the gateway (and does
    /// not throw): the refusal code is answered, the money is held on the ledger for an operator and
    /// not credited, and the replay neither refunds nor credits.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CardGatewayNotConfigured_RefusedSettlementHoldsThePaymentWithoutRefundAsync()
    {
        _cardGateway.IsConfigured = false;
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();
        var second = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(Payment(invoiceId, second));
        var replay = await InvokeAsync(Payment(invoiceId, second));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        replay.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        _cardGateway.RefundInstructions.Should().BeEmpty();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var row = await LedgerRowAsync(second);
        row.AddedToCredit.Should().BeFalse();
        row.AmountOut.Should().Be(0m);
    }

    /// <summary>The settlement writes the invoice's activity log line in the same save.</summary>
    [Fact]
    public async Task HandleAsync_UnpaidInvoice_RecordsTheMarkedPaidActivityAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));

        var result = await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityLogs.AsNoTracking()
            .AnyAsync(a => a.Description == $"Invoice Marked Paid - Invoice ID: {invoiceId}"))
            .Should().BeTrue();
    }

    /// <summary>On a part-paid invoice the ledger row carries only what was still due, not the total.</summary>
    [Fact]
    public async Task HandleAsync_PartPaidInvoice_RecordsOnlyTheAmountStillDueAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            context.Transactions.Add(invoice.AddPayment(
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"part-{Guid.NewGuid():N}", 5m, 0m, null, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var transactionId = $"pi_{Guid.NewGuid():N}";
        var result = await InvokeAsync(new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 15m, "USD"));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var check = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId))
            .AmountIn.Should().Be(15m);
    }

    /// <summary>
    /// The same payment settling the same invoice twice (a double click, a retry after a crash) is a
    /// replay: success, one ledger row, and nothing refunded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_SamePaymentForTheSameInvoice_IsAReplayAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        var transactionId = $"pi_{Guid.NewGuid():N}";
        (await InvokeAsync(Payment(invoiceId, transactionId))).IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(Payment(invoiceId, transactionId));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == transactionId)).Should().Be(1);
        _cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>
    /// A transaction already on the ledger for another invoice is refused with
    /// <c>PAYMENT_INTENT_ALREADY_USED</c>, and nothing is refunded: that money is recorded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_TransactionUsedByAnotherInvoice_RefusesWithoutRefundAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = $"pi_{Guid.NewGuid():N}";
        (await InvokeAsync(Payment(await SeedUnpaidInvoiceAsync(clientId), transactionId)))
            .IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(Payment(await SeedUnpaidInvoiceAsync(clientId), transactionId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        _cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>An id that names no invoice is refused with <c>INVOICE_NOT_FOUND</c>, and the payment is refunded.</summary>
    [Fact]
    public async Task HandleAsync_UnknownInvoice_RefusesNotFoundAndRefundsAsync()
    {
        var result = await InvokeAsync(Payment(BillingTestSeeds.NoSuchId, "pi_none"));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        _cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction("pi_none", InvoiceTotal, "USD", "return-pi_none"));
    }

    /// <summary>
    /// An invoice another payment already settled cannot record this one: <c>INVOICE_NOT_PAYABLE</c>,
    /// and the money is refunded to the card.
    /// </summary>
    [Fact]
    public async Task HandleAsync_InvoiceNoLongerPayable_RefusesAndRefundsAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();
        var second = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(Payment(invoiceId, second));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        _cardGateway.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(second);
    }

    /// <summary>
    /// A payment of another amount than what is due is refused with <c>PAYMENT_INTENT_MISMATCH</c>,
    /// refunded, and the invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AmountOtherThanDue_RefusesAndRefundsAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 25m, "USD"));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        _cardGateway.RefundInstructions.Should().ContainSingle().Which.Amount.Should().Be(25m);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// When the gateway refuses to refund a payment the invoice cannot record, the money goes to the
    /// client's credit balance, so it is never left taken and unrecorded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_RefundRefused_CreditsTheClientBalanceAsync()
    {
        _cardGateway.RefusesRefunds = true;
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(InvoiceTotal);
    }

    /// <summary>
    /// Another writer closing the invoice between this handler's read and its save is a concurrency
    /// conflict the handler catches: the payment it was given is refunded and the refusal is
    /// <c>INVOICE_NOT_PAYABLE</c>, never an unhandled exception with the money kept.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ConcurrentChangeByAnotherWriter_RefundsInsteadOfThrowingAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = new SettleInvoiceCardPaymentHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IActivityLogger>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            _cardGateway,
            scope.ServiceProvider.GetRequiredService<IClientCredit>(),
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            NullLogger<SettleInvoiceCardPaymentHandler>.Instance);

        // The handler's context holds the row before the competing write, so its own load moments
        // later hands back this tracked, pre-conflict version and its save conflicts.
        _ = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        using (var otherScope = factory.Services.CreateScope())
        {
            var otherContext = otherScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            // A cancel, not another payment: a payment row would be fixed up into the handler's
            // tracked invoice on its reload and refuse on the amount before any save is tried.
            var other = await otherContext.Invoices.FirstAsync(i => i.Id == invoiceId);
            other.Cancel();
            await otherContext.SaveChangesAsync(CancellationToken.None);
        }

        var transactionId = $"pi_{Guid.NewGuid():N}";
        var result = await handler.HandleAsync(Payment(invoiceId, transactionId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        _cardGateway.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(transactionId);
    }

    /// <summary>
    /// A refunded payment is written to the ledger under its own reference, in and out again and on
    /// no invoice, so its replay is refused as already used and the card is not refunded twice.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ReplayOfARefundedPayment_DoesNotRefundAgainAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();
        var second = $"pi_{Guid.NewGuid():N}";
        (await InvokeAsync(Payment(invoiceId, second))).ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);

        var replay = await InvokeAsync(Payment(invoiceId, second));

        replay.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        _cardGateway.RefundInstructions.Should().ContainSingle(r => r.TransactionId == second);
        var row = await LedgerRowAsync(second);
        row.InvoiceId.Should().BeNull();
        row.AmountIn.Should().Be(InvoiceTotal);
        row.AmountOut.Should().Be(InvoiceTotal);
        row.AddedToCredit.Should().BeFalse();
    }

    /// <summary>
    /// A payment credited to the balance after a refused refund is written to the ledger, so its
    /// replay credits nothing more and the balance holds the payment once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ReplayOfACreditedPayment_CreditsOnceAsync()
    {
        _cardGateway.RefusesRefunds = true;
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();
        var second = $"pi_{Guid.NewGuid():N}";
        (await InvokeAsync(Payment(invoiceId, second))).ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);

        var replay = await InvokeAsync(Payment(invoiceId, second));

        replay.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(InvoiceTotal);
        _cardGateway.RefundInstructions.Should().ContainSingle(r => r.TransactionId == second);
        var row = await LedgerRowAsync(second);
        row.AddedToCredit.Should().BeTrue();
        row.AmountIn.Should().Be(InvoiceTotal);
        row.AmountOut.Should().Be(0m);
    }

    /// <summary>
    /// A charge in another currency than the client's billing currency is not credited when its
    /// refund is refused: the balance would take it at the wrong value. It is held on the ledger for
    /// an operator instead.
    /// </summary>
    [Fact]
    public async Task HandleAsync_RefundRefusedForAChargeInAnotherCurrency_HoldsItForAnOperatorAsync()
    {
        _cardGateway.RefusesRefunds = true;
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.Clients.SingleAsync(c => c.Id == clientId)).SetCurrency("EUR");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 25m, "USD"));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var row = await LedgerRowAsync(transactionId);
        row.AddedToCredit.Should().BeFalse();
        row.AmountIn.Should().Be(25m);
        row.AmountOut.Should().Be(0m);
    }

    /// <summary>
    /// A refund the gateway fails to answer leaves its outcome unknown: the handler does not throw
    /// past it or credit on top of a possible refund, it holds the payment on the ledger for an
    /// operator, and the replay neither refunds nor credits.
    /// </summary>
    [Fact]
    public async Task HandleAsync_RefundWithNoAnswer_HoldsThePaymentForAnOperatorAsync()
    {
        _cardGateway.ThrowsOnRefundCall = 1;
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        (await InvokeAsync(Payment(invoiceId, $"pi_{Guid.NewGuid():N}"))).IsSuccess.Should().BeTrue();
        var second = $"pi_{Guid.NewGuid():N}";

        var result = await InvokeAsync(Payment(invoiceId, second));
        var replay = await InvokeAsync(Payment(invoiceId, second));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        replay.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        _cardGateway.RefundInstructions.Should().ContainSingle();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var row = await LedgerRowAsync(second);
        row.AddedToCredit.Should().BeFalse();
        row.AmountOut.Should().Be(0m);
    }

    /// <summary>
    /// A conflict from an unrelated edit that leaves the invoice open for this payment is retried on
    /// the fresh row: the invoice is settled and nothing is refunded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ConcurrentUnrelatedEdit_SettlesOnTheRetryAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = new SettleInvoiceCardPaymentHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IActivityLogger>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            _cardGateway,
            scope.ServiceProvider.GetRequiredService<IClientCredit>(),
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            NullLogger<SettleInvoiceCardPaymentHandler>.Instance);

        // The handler's context holds the pre-conflict row, so its first save conflicts.
        _ = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        using (var otherScope = factory.Services.CreateScope())
        {
            var otherContext = otherScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await otherContext.Invoices.FirstAsync(i => i.Id == invoiceId)).UpdateNotes("Edited by staff");
            await otherContext.SaveChangesAsync(CancellationToken.None);
        }

        var transactionId = $"pi_{Guid.NewGuid():N}";
        var result = await handler.HandleAsync(Payment(invoiceId, transactionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _cardGateway.RefundInstructions.Should().BeEmpty();
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId))
            .InvoiceId.Should().Be(invoiceId);
    }

    /// <summary>
    /// Another request settles the invoice with this very transaction between this settlement's
    /// ledger read and its invoice load, so the invoice it then loads is already paid and refuses the
    /// payment with <c>INVOICE_NOT_PAYABLE</c>. The payment is recorded, so this is a replay: success,
    /// one ledger row, nothing refunded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_SettledInlineWithTheSameTransactionAtTheLoad_AnswersSuccessWithoutRefundAsync()
    {
        var invoiceId = await SeedUnpaidInvoiceAsync(await BillingTestSeeds.SeedClientAsync(factory));
        var transactionId = $"pi_{Guid.NewGuid():N}";
        var hook = new InvoiceLoadHookInterceptor(async () =>
            (await InvokeAsync(Payment(invoiceId, transactionId))).IsSuccess.Should().BeTrue());
        _host = BuildHost(hook);

        var result = await InvokeAsync(Payment(invoiceId, transactionId));

        result.IsSuccess.Should().BeTrue();
        hook.Fired.Should().BeTrue("the other request must have settled between the ledger read and the invoice load");
        hook.LedgerReadsBeforeFire.Should().Be(1);
        _cardGateway.RefundInstructions.Should().BeEmpty();
        (await LedgerRowAsync(transactionId)).InvoiceId.Should().Be(invoiceId);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>
    /// Another request records the transaction for a different invoice between this settlement's
    /// ledger read and its invoice load, and this settlement is then refused (the amount is not what is
    /// due). Handing the payment back would refund money the ledger holds, so the refusal answers
    /// <c>PAYMENT_INTENT_ALREADY_USED</c> and the gateway is not asked to refund. This is also the
    /// realistic cover of a <c>PAYMENT_INTENT_MISMATCH</c> refusal reached with the money already
    /// recorded: no other setup reaches it, because a settlement records only what is still due and a
    /// card row for part of an invoice cannot be produced by the domain.
    /// </summary>
    [Fact]
    public async Task ReturnPayment_WhenTheTransactionIsRecordedForAnotherInvoice_AnswersAlreadyUsedWithoutRefundAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var otherInvoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var transactionId = $"pi_{Guid.NewGuid():N}";
        var hook = new InvoiceLoadHookInterceptor(async () =>
            (await InvokeAsync(Payment(otherInvoiceId, transactionId))).IsSuccess.Should().BeTrue());
        _host = BuildHost(hook);

        var result = await InvokeAsync(new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 25m, "USD"));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        hook.Fired.Should().BeTrue("the other request must have recorded the transaction between the ledger read and the invoice load");
        hook.LedgerReadsBeforeFire.Should().Be(1);
        _cardGateway.RefundInstructions.Should().BeEmpty();
        (await LedgerRowAsync(transactionId)).InvoiceId.Should().Be(otherInvoiceId);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// A save that conflicts because another writer changed the invoice, while that writer's request also
    /// recorded this transaction for another invoice, is answered on the reload as <c>PAYMENT_INTENT_ALREADY_USED</c>:
    /// the money is on the ledger, so the gateway is not asked to refund it.
    /// </summary>
    [Fact]
    public async Task ReloadAfterConflict_WhenTheTransactionIsRecordedForAnotherInvoice_AnswersAlreadyUsedWithoutRefundAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var otherInvoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var (result, hook) = await SettleAfterCompetingWriteAsync(
            invoiceId, transactionId, () => InvokeAsync(Payment(otherInvoiceId, transactionId)));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        hook.Fired.Should().BeTrue();
        hook.LedgerReadsBeforeFire.Should().Be(1);
        _cardGateway.RefundInstructions.Should().BeEmpty();
        (await LedgerRowAsync(transactionId)).InvoiceId.Should().Be(otherInvoiceId);
    }

    /// <summary>
    /// The same conflict where the transaction was meanwhile written as a returned payment (a row on no
    /// invoice, as a refund leaves): the reload answers <c>PAYMENT_INTENT_ALREADY_USED</c> and nothing is refunded.
    /// </summary>
    [Fact]
    public async Task ReloadAfterConflict_WhenTheTransactionIsRecordedAsAReturnRow_AnswersAlreadyUsedWithoutRefundAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var transactionId = $"pi_{Guid.NewGuid():N}";

        var (result, hook) = await SettleAfterCompetingWriteAsync(
            invoiceId,
            transactionId,
            async () =>
            {
                using var other = factory.Services.CreateScope();
                var context = other.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
                context.Transactions.Add(Transaction.Create(
                    clientId, DateTimeOffset.UtcNow, "Card payment refunded", transactionId, null,
                    PaymentMethodNames.Card, InvoiceTotal, InvoiceTotal, 0m, addedToCredit: false, "USD", TransactionSource.Manual));
                await context.SaveChangesAsync(CancellationToken.None);
            });

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        hook.Fired.Should().BeTrue();
        hook.LedgerReadsBeforeFire.Should().Be(1);
        _cardGateway.RefundInstructions.Should().BeEmpty();
        (await LedgerRowAsync(transactionId)).InvoiceId.Should().BeNull();
    }

    /// <summary>
    /// The row that records a returned payment is saved while another request settles another invoice
    /// with the same card transaction: the unique gateway payment index refuses the returned-payment row
    /// and the handler stands down. The ledger keeps the one row the other request wrote, and a repeat of
    /// the payment is a success for the invoice it settled and <c>PAYMENT_INTENT_ALREADY_USED</c> for any other.
    /// </summary>
    [Fact]
    public async Task RecordReturn_WhenTheTransactionIsOnTheLedgerAfterTheSaveIsRefused_StandsDownAndReplaysAnswerFromTheLedgerAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var otherInvoiceId = await SeedUnpaidInvoiceAsync(clientId);
        var transactionId = $"pi_{Guid.NewGuid():N}";
        var hook = new LedgerRowSaveHookInterceptor(
            row => row.TransactionId == transactionId && row.InvoiceId is null,
            async () => (await InvokeAsync(Payment(otherInvoiceId, transactionId))).IsSuccess.Should().BeTrue());
        _host = BuildHost(hook);

        // The amount is not what is due, so the payment is handed back and the returned-payment row is saved.
        var result = await InvokeAsync(new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 25m, "USD"));
        var sameInvoice = await InvokeAsync(Payment(otherInvoiceId, transactionId));
        var anotherInvoice = await InvokeAsync(Payment(invoiceId, transactionId));

        hook.Fired.Should().BeTrue("the other request must have settled the payment while the returned-payment row was saving");
        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        sameInvoice.IsSuccess.Should().BeTrue();
        anotherInvoice.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        var row = await LedgerRowAsync(transactionId);
        row.InvoiceId.Should().Be(otherInvoiceId);
        row.Source.Should().Be(TransactionSource.GatewayPayment);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// Runs the settlement of an invoice whose save is made to conflict, with <paramref name="record"/>
    /// executed by the hook right after the settlement read the ledger empty: the handler's context
    /// already holds the invoice, the hook records the transaction and edits the invoice, so the save
    /// conflicts and the reload finds the transaction on the ledger.
    /// </summary>
    /// <param name="invoiceId">The invoice being settled.</param>
    /// <param name="transactionId">The card transaction of the payment.</param>
    /// <param name="record">What the competing writer does with the transaction.</param>
    /// <returns>The handler's result and the hook, whose <see cref="InvoiceLoadHookInterceptor.Fired"/> proves the race happened.</returns>
    private async Task<(Result Result, InvoiceLoadHookInterceptor Hook)> SettleAfterCompetingWriteAsync(
        int invoiceId, string transactionId, Func<Task> record)
    {
        var hook = new InvoiceLoadHookInterceptor(async () =>
        {
            await record();
            using var other = factory.Services.CreateScope();
            var otherContext = other.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await otherContext.Invoices.FirstAsync(i => i.Id == invoiceId)).UpdateNotes("Edited by staff");
            await otherContext.SaveChangesAsync(CancellationToken.None);
        });
        _host = BuildHost(hook);
        using var scope = _host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = new SettleInvoiceCardPaymentHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IActivityLogger>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            _cardGateway,
            scope.ServiceProvider.GetRequiredService<IClientCredit>(),
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            NullLogger<SettleInvoiceCardPaymentHandler>.Instance);

        // The handler's context holds the pre-conflict invoice, so its own load moments later hands
        // back this tracked version and its save conflicts once the hook has edited the row.
        _ = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        var result = await handler.HandleAsync(Payment(invoiceId, transactionId), CancellationToken.None);
        return (result, hook);
    }

    /// <summary>Reads the one ledger row written under a payment reference.</summary>
    /// <param name="transactionId">The card gateway's transaction id.</param>
    /// <returns>The ledger row.</returns>
    private async Task<Transaction> LedgerRowAsync(string transactionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId);
    }

    /// <summary>A card payment of the seeded invoice's full total.</summary>
    /// <param name="invoiceId">The invoice the payment settles.</param>
    /// <param name="transactionId">The card gateway's transaction id.</param>
    /// <returns>The command.</returns>
    private static SettleInvoiceCardPaymentCommand Payment(int invoiceId, string transactionId) =>
        new(invoiceId, transactionId, InvoiceTotal, "USD");

    /// <summary>Seeds an unpaid USD invoice of <see cref="InvoiceTotal"/> for a client.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedUnpaidInvoiceAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", InvoiceTotal, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Dispatches the command on a fresh scope's bus, with refunds going to <see cref="_cardGateway"/>.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(SettleInvoiceCardPaymentCommand cmd)
    {
        _host ??= BuildHost();
        using var scope = _host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }

    /// <summary>Builds a host whose card gateway is <see cref="_cardGateway"/> and whose data context carries <paramref name="interceptors"/>.</summary>
    /// <param name="interceptors">Interceptors every data context of the host is built with, to act at a chosen point of a settlement.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(params IInterceptor[] interceptors) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ICardPaymentGateway>(_cardGateway);
                if (interceptors.Length > 0)
                {
                    services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(interceptors));
                }
            }));
}
