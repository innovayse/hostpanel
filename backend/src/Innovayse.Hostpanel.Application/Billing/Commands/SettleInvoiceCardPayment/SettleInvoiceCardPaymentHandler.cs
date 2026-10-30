using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;

/// <summary>
/// Handles <see cref="SettleInvoiceCardPaymentCommand"/>: lets the invoice settle itself with a card
/// payment the gateway has already taken, and saves its ledger row, the Paid mark and the activity
/// log line together. Every card payment that settles an invoice comes through here, whether the
/// client paid an order at checkout or one of their invoices with a saved card.
/// </summary>
/// <remarks>
/// The money is already taken when this runs, so every refusal that leaves the payment unrecorded
/// hands it back: the charge is refunded to the card, and when the gateway refuses the refund the
/// amount goes to the client's credit balance instead. A refund to the card comes first because the
/// payer asked to pay this invoice, not to hold a balance; the balance is the fallback that still
/// keeps the money on the books. A replay of a payment this invoice already recorded (a double click,
/// a retried request) answers success without touching anything.
/// <para>
/// What happens to money handed back is written to the ledger under the card transaction's own
/// reference, so a replay finds it there and never refunds or credits a second time: a refund is a
/// row taking the amount in and out again, a credit is a row adding it to the balance, and a return
/// whose outcome is unknown (the gateway gave no answer to the refund) or that nothing could take is
/// a row holding the amount for an operator, raised as a critical log line. The refund itself is
/// keyed on the payment, so a repeated return is the same refund at the gateway. The balance is only
/// credited in the client's billing currency; a charge in any other currency is left to an operator
/// rather than credited at the wrong value.
/// </para>
/// </remarks>
public sealed class SettleInvoiceCardPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for the settlement, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Card payment gateway, which refunds a payment the invoice cannot record.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Puts a payment the gateway would not refund on the client's credit balance.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Wolverine bus the client's billing currency is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SettleInvoiceCardPaymentHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for the settlement, saved with the change it describes.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="cardGateway">Card payment gateway, which refunds a payment the invoice cannot record.</param>
    /// <param name="clientCredit">Puts a payment the gateway would not refund on the client's credit balance.</param>
    /// <param name="bus">Wolverine bus the client's billing currency is read through.</param>
    /// <param name="logger">Structured logger.</param>
    public SettleInvoiceCardPaymentHandler(
        IHostpanelDbContext context,
        IActivityLogger activity,
        IClock clock,
        ICardPaymentGateway cardGateway,
        IClientCredit clientCredit,
        IMessageBus bus,
        ILogger<SettleInvoiceCardPaymentHandler> logger)
    {
        _context = context;
        _activity = activity;
        _clock = clock;
        _cardGateway = cardGateway;
        _clientCredit = clientCredit;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The invoice and the verified card transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the invoice is settled, or when this same payment already settled it;
    /// <c>INVOICE_NOT_FOUND</c> when the id names no invoice; <c>PAYMENT_INTENT_ALREADY_USED</c> when
    /// the transaction is already on the ledger for another payment; <c>INVOICE_NOT_PAYABLE</c> when
    /// the invoice is not open for payment, or another writer changed it first; or
    /// <c>PAYMENT_INTENT_MISMATCH</c> when the amount or currency taken is not what the invoice owes.
    /// Every refusal but <c>PAYMENT_INTENT_ALREADY_USED</c> hands the money back first.
    /// </returns>
    public async Task<Result> HandleAsync(SettleInvoiceCardPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The ledger is read before the invoice: the row and the Paid mark are saved together, so an
        // invoice read after a row was seen is never older than that row.
        var recordedFor = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionId == cmd.TransactionId)
            .Select(t => t.InvoiceId)
            .ToListAsync(ct);

        var invoice = await LoadInvoiceAsync(cmd.InvoiceId, ct);

        if (recordedFor.Count > 0)
        {
            // The money is already on the ledger (settled, or handed back), so nothing is handed
            // back again. When it settled this very invoice the request is a replay and its outcome
            // is the first one's.
            return invoice is { IsSettled: true } && recordedFor.Contains(invoice.Id)
                ? Result.Success()
                : Result.FailureCode(HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
        }

        if (invoice is null)
        {
            return await ReturnPaymentAsync(cmd, null, HostpanelErrorCodes.INVOICE_NOT_FOUND, ct);
        }

        var refusal = Refusal(cmd, invoice);
        if (refusal is not null)
        {
            var reloaded = await ReloadAfterConflictAsync(cmd, ct);
            if (reloaded.Settled)
            {
                return Result.Success();
            }

            return await ReturnPaymentAsync(cmd, reloaded.Invoice ?? invoice, refusal, ct);
        }

        if (await TrySettleAsync(cmd, invoice, ct))
        {
            return Result.Success();
        }

        // Another writer changed the invoice between the read and this save: a concurrent request
        // settling it with this same payment, a different payment, an admin, or an unrelated edit.
        // An unrelated edit leaves the invoice open for this payment, so the settlement is tried
        // once more on the fresh row before the money is handed back.
        var current = await ReloadAfterConflictAsync(cmd, ct);
        if (current.Settled)
        {
            return Result.Success();
        }

        if (current.Invoice is not null && Refusal(cmd, current.Invoice) is null)
        {
            if (await TrySettleAsync(cmd, current.Invoice, ct))
            {
                return Result.Success();
            }

            current = await ReloadAfterConflictAsync(cmd, ct);
            if (current.Settled)
            {
                return Result.Success();
            }
        }

        return await ReturnPaymentAsync(cmd, current.Invoice ?? invoice, HostpanelErrorCodes.INVOICE_NOT_PAYABLE, ct);
    }

    /// <summary>Loads the invoice with its payments, which the ledger row needs to record only what is still due.</summary>
    /// <param name="invoiceId">The invoice id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked invoice, or <see langword="null"/> when the id names none.</returns>
    private async Task<Invoice?> LoadInvoiceAsync(int invoiceId, CancellationToken ct)
    {
        return await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
    }

    /// <summary>
    /// Drops the changes a conflicting save left staged and reads the invoice as the database holds it.
    /// </summary>
    /// <param name="cmd">The payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether a concurrent request already settled the invoice with this payment, and the fresh invoice.</returns>
    private async Task<SettleReload> ReloadAfterConflictAsync(SettleInvoiceCardPaymentCommand cmd, CancellationToken ct)
    {
        _context.DetachAll();
        var settled = await _context.Transactions.AsNoTracking()
            .AnyAsync(t => t.TransactionId == cmd.TransactionId && t.InvoiceId == cmd.InvoiceId, ct);
        return new SettleReload(settled, settled ? null : await LoadInvoiceAsync(cmd.InvoiceId, ct));
    }

    /// <summary>Why the invoice cannot record this payment, if it cannot.</summary>
    /// <param name="cmd">The payment.</param>
    /// <param name="invoice">The invoice it pays.</param>
    /// <returns>
    /// <c>INVOICE_NOT_PAYABLE</c> when the invoice is not open for payment, <c>PAYMENT_INTENT_MISMATCH</c>
    /// when the amount or currency is not what is due now, or <see langword="null"/> when it can be recorded.
    /// </returns>
    private static string? Refusal(SettleInvoiceCardPaymentCommand cmd, Invoice invoice)
    {
        if (!invoice.IsPayable)
        {
            return HostpanelErrorCodes.INVOICE_NOT_PAYABLE;
        }

        // The settlement records what is due now; a payment of any other amount would leave the
        // ledger disagreeing with the card, so it is handed back rather than recorded.
        if (!string.Equals(cmd.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase)
            || CurrencyCode.ToMinorUnits(cmd.Amount, cmd.Currency) != CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency))
        {
            return HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH;
        }

        return null;
    }

    /// <summary>Settles the invoice with the payment and saves the ledger row, the Paid mark and the log line together.</summary>
    /// <param name="cmd">The payment.</param>
    /// <param name="invoice">The tracked, payable invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="false"/> when another writer changed the invoice first.</returns>
    private async Task<bool> TrySettleAsync(SettleInvoiceCardPaymentCommand cmd, Invoice invoice, CancellationToken ct)
    {
        var ledgerRow = invoice.Settle(
            PaymentMethodNames.Card, cmd.TransactionId, _clock.UtcNow);
        if (ledgerRow is not null)
        {
            _context.Transactions.Add(ledgerRow);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Invoice Marked Paid - Invoice ID: {invoice.Id}"), ct);

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }

    /// <summary>
    /// Hands back a card payment the invoice cannot record: refunds it to the card, or, when the
    /// gateway refuses the refund, puts it on the client's credit balance in their billing currency.
    /// The outcome is written to the ledger under the payment's reference so a replay never repeats it.
    /// </summary>
    /// <param name="cmd">The payment that could not be recorded.</param>
    /// <param name="invoice">The invoice, or <see langword="null"/> when it does not exist.</param>
    /// <param name="errorCode">Why the payment could not be recorded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refusal carrying <paramref name="errorCode"/>.</returns>
    private async Task<Result> ReturnPaymentAsync(
        SettleInvoiceCardPaymentCommand cmd, Invoice? invoice, string errorCode, CancellationToken ct)
    {
        CardRefundResult refund;
        try
        {
            refund = await _cardGateway.RefundAsync(
                new CardRefundInstruction(cmd.TransactionId, cmd.Amount, cmd.Currency, $"return-{cmd.TransactionId}"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing is known about the money: the refund may or may not have been issued. It is
            // held on the ledger for an operator, never credited blind on top of a possible refund.
            _logger.LogCritical(
                ex,
                "Card payment {TransactionId} of {Amount} {Currency} for invoice {InvoiceId} could not be recorded ({Reason}) and its refund got no answer; held for an operator.",
                cmd.TransactionId, cmd.Amount, cmd.Currency, cmd.InvoiceId, errorCode);
            await RecordReturnAsync(cmd, invoice, CardReturnOutcome.HeldForOperator, ct);
            return Result.FailureCode(errorCode);
        }

        if (refund.Success)
        {
            _logger.LogWarning(
                "Card payment {TransactionId} for invoice {InvoiceId} could not be recorded ({Reason}); refunded as {RefundId}.",
                cmd.TransactionId, cmd.InvoiceId, errorCode, refund.RefundId);
            await RecordReturnAsync(cmd, invoice, CardReturnOutcome.Refunded, ct);
            return Result.FailureCode(errorCode);
        }

        if (invoice is null
            || !await _bus.IsClientBillingCurrencyAsync(invoice, cmd.Currency, ct)
            || !await CreditToBalanceAsync(cmd, invoice, ct))
        {
            // Neither the card nor the client's balance can take the money back (no client, a charge
            // in another currency than the balance is kept in, or the balance would not save); an
            // operator has to.
            _logger.LogCritical(
                "Card payment {TransactionId} of {Amount} {Currency} for invoice {InvoiceId} could not be recorded ({Reason}), refunded or credited; held for an operator.",
                cmd.TransactionId, cmd.Amount, cmd.Currency, cmd.InvoiceId, errorCode);
            await RecordReturnAsync(cmd, invoice, CardReturnOutcome.HeldForOperator, ct);
            return Result.FailureCode(errorCode);
        }

        _logger.LogWarning(
            "Card payment {TransactionId} for invoice {InvoiceId} could not be recorded ({Reason}) nor refunded; credited to client {ClientId}.",
            cmd.TransactionId, cmd.InvoiceId, errorCode, invoice.ClientId);
        return Result.FailureCode(errorCode);
    }

    /// <summary>
    /// Puts the card payment on the client's balance and saves it with its activity entry and its
    /// ledger row. The money is already taken, so a concurrent change to the balance is retried once
    /// on the fresh row (an addition is safe to repeat after a save that wrote nothing) rather than
    /// answered 409.
    /// </summary>
    /// <param name="cmd">The payment that could not be recorded.</param>
    /// <param name="invoice">The invoice whose client is credited.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the balance now holds the payment.</returns>
    private async Task<bool> CreditToBalanceAsync(SettleInvoiceCardPaymentCommand cmd, Invoice invoice, CancellationToken ct)
    {
        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Card payment {cmd.TransactionId} credited to balance - Invoice ID: {cmd.InvoiceId}"), ct);
        _context.Transactions.Add(ReturnRow(cmd, invoice, CardReturnOutcome.Credited));
        try
        {
            if ((await _clientCredit.AddAndSaveAsync(new ClientCreditChange(invoice.ClientId, cmd.Amount), ct)).IsSuccess)
            {
                return true;
            }
        }
        catch (ConcurrencyConflictException)
        {
            // The balance moved again during the one retry; the caller hands this to an operator.
        }

        // Nothing was saved: the staged row and log line are dropped before the operator's row is written.
        _context.DetachAll();
        return false;
    }

    /// <summary>
    /// Writes the returned payment to the ledger under its own reference and saves it. Without a
    /// client (the invoice does not exist) there is no ledger to write to, and the log line already
    /// raised is the record.
    /// </summary>
    /// <param name="cmd">The returned payment.</param>
    /// <param name="invoice">The invoice, or <see langword="null"/> when it does not exist.</param>
    /// <param name="outcome">What happened to the money.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task RecordReturnAsync(
        SettleInvoiceCardPaymentCommand cmd, Invoice? invoice, CardReturnOutcome outcome, CancellationToken ct)
    {
        if (invoice is null)
        {
            return;
        }

        _context.Transactions.Add(ReturnRow(cmd, invoice, outcome));
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Shapes the ledger row of a returned payment. It is not linked to the invoice, because none of
    /// this money pays it; its reference is the payment's, which is what a replay looks up.
    /// </summary>
    /// <param name="cmd">The returned payment.</param>
    /// <param name="invoice">The invoice the payment was meant for.</param>
    /// <param name="outcome">What happened to the money.</param>
    /// <returns>The row: in and out again for a refund, into the balance for a credit, held otherwise.</returns>
    private Transaction ReturnRow(SettleInvoiceCardPaymentCommand cmd, Invoice invoice, CardReturnOutcome outcome)
    {
        var description = outcome switch
        {
            CardReturnOutcome.Refunded => $"Card payment for Invoice #{invoice.Id} refunded",
            CardReturnOutcome.Credited => $"Card payment for Invoice #{invoice.Id} credited to balance",
            _ => $"Card payment for Invoice #{invoice.Id} held for operator review",
        };

        return Transaction.Create(
            invoice.ClientId,
            _clock.UtcNow,
            description,
            cmd.TransactionId,
            null,
            PaymentMethodNames.Card,
            cmd.Amount,
            outcome == CardReturnOutcome.Refunded ? cmd.Amount : 0m,
            0m,
            addedToCredit: outcome == CardReturnOutcome.Credited,
            cmd.Currency);
    }
}
