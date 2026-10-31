using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;
namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
/// Handles <see cref="CompleteGatewayPaymentCommand"/>. Called from the payer's
/// return page and from the reconciliation job: both paths converge here, so
/// idempotency is load-bearing.
/// </summary>
public sealed class CompleteGatewayPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the configured payment plugin for a module id.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>Wolverine message bus, used to trigger the linked order's fulfillment.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<CompleteGatewayPaymentHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Puts what the gateway took beyond the amount due on the client's credit balance.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Resolves the configured payment plugin for a module id.</param>
    /// <param name="bus">Wolverine message bus.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="clientCredit">Puts what the gateway took beyond the amount due on the client's credit balance.</param>
    public CompleteGatewayPaymentHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        IMessageBus bus,
        ILogger<CompleteGatewayPaymentHandler> logger,
        IClock clock,
        IClientCredit clientCredit)
    {
        _context = context;
        _pluginResolver = pluginResolver;
        _bus = bus;
        _logger = logger;
        _clock = clock;
        _clientCredit = clientCredit;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The complete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The completion state, or <c>INVOICE_NOT_FOUND</c>, <c>GATEWAY_SESSION_NOT_FOUND</c>,
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>, or <c>GATEWAY_PAYMENT_MISMATCH</c> when the gateway
    /// reports a paid amount below what the invoice owes, or another currency (the invoice stays
    /// unpaid). A paid amount above what is owed (a payment recorded while the session was open)
    /// settles the invoice and puts the rest on the client's credit balance, and staff are alerted after the
    /// save that credited it, never twice. A paid status without
    /// an amount is a mismatch. When another path settled or closed the invoice while the session
    /// was open, whatever the session took that the ledger does not hold goes to the client's
    /// balance (<c>INVOICE_NOT_PAYABLE</c> when the invoice was closed unpaid).
    /// </returns>
    public async Task<Result<GatewayCompletionDto>> HandleAsync(
        CompleteGatewayPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Payments are loaded because the gateway's amount is compared with what is still due.
        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<GatewayCompletionDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (invoice.IsSettled)
        {
            return await CompleteSettledAsync(invoice, ct);
        }

        if (invoice.GatewayModule is null || invoice.GatewayOrderId is null)
        {
            return Result<GatewayCompletionDto>.FailureCode(HostpanelErrorCodes.GATEWAY_SESSION_NOT_FOUND);
        }

        var plugin = await _pluginResolver.ResolveAsync(invoice.GatewayModule, ct);
        if (plugin is null)
        {
            return Result<GatewayCompletionDto>.FailureCode(HostpanelErrorCodes.PAYMENT_PLUGIN_NOT_CONFIGURED);
        }

        var status = await plugin.GetStatusAsync(invoice.GatewayOrderId, ct);

        if (status.State != GatewayPaymentState.Paid)
        {
            _logger.LogInformation(
                "Invoice {InvoiceId} gateway session {GatewayOrderId} is {State} ({Raw}).",
                invoice.Id, invoice.GatewayOrderId, status.State, status.RawStatus);
            return Result<GatewayCompletionDto>.Success(new GatewayCompletionDto(
                status.State == GatewayPaymentState.Pending
                    ? GatewayCompletionState.Pending
                    : GatewayCompletionState.Declined));
        }

        var dueMinor = CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency);

        // A paid status with no amount says nothing about what was taken, so it is not a settlement.
        if (status.AmountMinor is not { } paidMinor || paidMinor < dueMinor || !await MatchesCurrencyAsync(invoice, status, ct))
        {
            _logger.LogWarning(
                "Invoice {InvoiceId} gateway session {GatewayOrderId} reports {Amount} in {Currency}, which is not what the invoice owes; not marked paid.",
                invoice.Id, invoice.GatewayOrderId, status.AmountMinor, status.CurrencyNumeric);
            return Result<GatewayCompletionDto>.FailureCode(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        }

        var gatewayTransactionId = status.TransactionId ?? invoice.GatewayOrderId;

        // The ledger row is saved with the Paid mark; the concurrent-completion guard below then
        // makes "ledger row exists" and "invoice is Paid" one atomic fact.
        var module = invoice.GatewayModule;
        var ledgerRow = invoice.Settle(
            module, gatewayTransactionId, _clock.UtcNow);
        if (ledgerRow is not null)
        {
            _context.Transactions.Add(ledgerRow);
        }

        // The session was opened for the amount due then; a payment recorded while it was open
        // lowered what is due now, so the gateway took more than the settlement records. The rest
        // belongs to the client and is kept on their balance, in the same save.
        var surplus = CurrencyCode.FromMinorUnits(Math.Max(paidMinor - dueMinor, 0L), invoice.Currency);
        if (paidMinor > dueMinor)
        {
            var credited = await _clientCredit.AddAsync(new ClientCreditChange(invoice.ClientId, surplus), ct);
            if (credited.IsFailure)
            {
                return Result<GatewayCompletionDto>.FailureCode(credited.ErrorCode!);
            }
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another writer changed the invoice between our read and our write: the result-page
            // auto-check racing the reconciler or a retried click completing this same payment, an
            // admin recording another payment, or an admin cancelling it. The failed changes are
            // dropped so the invoice and the ledger are read as the database holds them.
            _context.DetachAll();
            var outcome = await CompleteAfterConflictAsync(cmd.InvoiceId, module, gatewayTransactionId, status, ct);
            if (outcome is null)
            {
                throw;
            }

            return outcome;
        }

        // Published only after the save, like the reconciler's stalled alert: a save that lost to a
        // conflict above publishes nothing, and a replay finds the ledger row and publishes nothing,
        // so the alert is never sent twice.
        if (paidMinor > dueMinor)
        {
            await _bus.PublishAsync(new GatewayPaymentCreditedEvent(
                invoice.Id, invoice.ClientId, module, gatewayTransactionId, surplus, invoice.Currency, Credited: true)
            {
                OccurredAt = _clock.UtcNow,
            });
        }

        await _bus.InvokeAsync(new FulfillInvoicedOrderCommand(invoice.Id), ct);

        return Result<GatewayCompletionDto>.Success(new GatewayCompletionDto(GatewayCompletionState.Paid));
    }

    /// <summary>
    /// Answers for an invoice that is already settled. When it still holds a session no payment was
    /// recorded through (another path settled it while the session was open), the gateway is asked
    /// whether that session took money too, and money it took that the ledger does not hold is put
    /// on the client's balance. The linked order is fulfilled either way: fulfilment is idempotent,
    /// and a completion whose save landed but whose fulfilment failed is finished by this replay.
    /// </summary>
    /// <param name="invoice">The settled invoice (tracked, payments loaded).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Paid completion state.</returns>
    private async Task<Result<GatewayCompletionDto>> CompleteSettledAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice is { GatewayModule: { } module, GatewayOrderId: { } orderId }
            && !invoice.WasPaidThrough(module))
        {
            var plugin = await _pluginResolver.ResolveAsync(module, ct);
            if (plugin is not null)
            {
                var status = await plugin.GetStatusAsync(orderId, ct);
                if (status.State == GatewayPaymentState.Paid)
                {
                    await HandBackUnrecordedAsync(invoice, module, status.TransactionId ?? orderId, status, ct);
                }
            }
        }

        await _bus.InvokeAsync(new FulfillInvoicedOrderCommand(invoice.Id), ct);
        return Result<GatewayCompletionDto>.Success(new GatewayCompletionDto(GatewayCompletionState.Paid));
    }

    /// <summary>
    /// Decides a paid completion whose save lost to another writer, from the invoice and ledger as
    /// the database now holds them.
    /// </summary>
    /// <param name="invoiceId">The invoice id.</param>
    /// <param name="module">The plugin id of the session that took the money.</param>
    /// <param name="reference">The gateway's reference of the payment.</param>
    /// <param name="status">The gateway's paid status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Paid when this payment is on the ledger (a concurrent completion) or another path settled the
    /// invoice (the money then goes to the balance); <c>INVOICE_NOT_PAYABLE</c> when the invoice was
    /// closed without payment, the money going to the balance; <see langword="null"/> when the
    /// invoice is still open, so the conflict is the caller's to raise.
    /// </returns>
    private async Task<Result<GatewayCompletionDto>?> CompleteAfterConflictAsync(
        int invoiceId, string module, string reference, GatewayPaymentStatus status, CancellationToken ct)
    {
        var current = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (current is null || current.IsPayable)
        {
            return null;
        }

        if (current.Transactions.Any(t => t.TransactionId == reference) || await _context.IsRecordedAsync(reference, ct))
        {
            _logger.LogInformation("Invoice {InvoiceId} was already marked paid by a concurrent completion.", invoiceId);
        }
        else
        {
            await HandBackUnrecordedAsync(current, module, reference, status, ct);
        }

        if (!current.IsSettled)
        {
            return Result<GatewayCompletionDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        await _bus.InvokeAsync(new FulfillInvoicedOrderCommand(current.Id), ct);
        return Result<GatewayCompletionDto>.Success(new GatewayCompletionDto(GatewayCompletionState.Paid));
    }

    /// <summary>
    /// Puts the whole amount a gateway session took on the client's credit balance when the invoice
    /// could not record it (another path settled it, or it was closed), the same rule as the part
    /// above what is due on a settlement. The money is written to the ledger under the gateway's
    /// reference in the same save, so a replay finds it there and never credits it twice, and staff are
    /// told after that save and never twice, through <see cref="GatewayPaymentCreditedEvent"/>. Money in
    /// another currency than the client's billing currency, or that the gateway reports without an
    /// amount, is not credited: it is written as held and raised as a critical log line for an operator.
    /// </summary>
    /// <param name="invoice">The invoice the session was opened for (tracked).</param>
    /// <param name="module">The plugin id of the session that took the money.</param>
    /// <param name="reference">The gateway's reference of the payment.</param>
    /// <param name="status">The gateway's paid status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the outcome is saved.</returns>
    private async Task HandBackUnrecordedAsync(
        Invoice invoice, string module, string reference, GatewayPaymentStatus status, CancellationToken ct)
    {
        // Recorded on the invoice's own payment history or on the ledger: the money is accounted for.
        if (invoice.Transactions.Any(t => t.TransactionId == reference) || await _context.IsRecordedAsync(reference, ct))
        {
            return;
        }

        if (status.AmountMinor is not { } paidMinor || paidMinor <= 0)
        {
            _logger.LogCritical(
                "Invoice {InvoiceId} gateway session {Reference} through {Module} reports paid with no amount while the invoice could not record it; an operator has to settle it.",
                invoice.Id, reference, module);
            return;
        }

        var amount = CurrencyCode.FromMinorUnits(paidMinor, invoice.Currency);
        var credit = await MatchesCurrencyAsync(invoice, status, ct)
            && await _bus.IsClientBillingCurrencyAsync(invoice, invoice.Currency, ct)
            && (await _clientCredit.AddAsync(new ClientCreditChange(invoice.ClientId, amount), ct)).IsSuccess;
        if (!credit)
        {
            _logger.LogCritical(
                "Invoice {InvoiceId} gateway payment {Reference} of {Amount} through {Module} could not be recorded or credited; held for an operator.",
                invoice.Id, reference, amount, module);
        }

        // Not linked to the invoice: none of this money pays it.
        _context.Transactions.Add(Transaction.Create(
            invoice.ClientId,
            _clock.UtcNow,
            credit
                ? $"Gateway payment for Invoice #{invoice.Id} credited to balance"
                : $"Gateway payment for Invoice #{invoice.Id} held for operator review",
            reference,
            null,
            module,
            amount,
            0m,
            0m,
            addedToCredit: credit,
            invoice.Currency));
        await _context.SaveChangesAsync(ct);

        // After the save, so a failed save repeats the whole hand-back and the alert is never sent
        // twice: the next pass finds the ledger row and returns above.
        await _bus.PublishAsync(new GatewayPaymentCreditedEvent(
            invoice.Id, invoice.ClientId, module, reference, amount, invoice.Currency, credit)
        {
            OccurredAt = _clock.UtcNow,
        });
    }

    /// <summary>
    /// Whether the gateway took the money in the invoice's currency. A currency the gateway does
    /// not report cannot be compared and is not held against it.
    /// </summary>
    /// <param name="invoice">The invoice being completed.</param>
    /// <param name="status">The gateway's paid status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the reported currency does not contradict the invoice.</returns>
    private async Task<bool> MatchesCurrencyAsync(Invoice invoice, GatewayPaymentStatus status, CancellationToken ct)
    {
        if (status.CurrencyNumeric is null)
        {
            return true;
        }

        var numeric = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == invoice.Currency)
            .Select(c => c.Numeric)
            .FirstOrDefaultAsync(ct);
        return numeric is not null
            && int.TryParse(numeric, out var expected)
            && int.TryParse(status.CurrencyNumeric, out var reported)
            && expected == reported;
    }
}
