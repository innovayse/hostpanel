using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.IssueCreditNote;

/// <summary>
/// Issues a credit note against a paid invoice and gives its value back to the client in the same operation: as account credit
/// through <see cref="IClientCredit"/>, or as a gateway refund through <see cref="RefundInvoicePaymentCommand"/>. The money moves
/// once, by the one mechanism, and the note records that it did.
/// </summary>
/// <remarks>
/// <para>
/// <b>Account credit.</b> The note, the invoice's <see cref="Invoice.CreditNotedAmount"/>, the balance and its ledger line are
/// saved together. Two notes racing on one invoice both change the invoice row, so the later save fails the row's concurrency
/// check instead of passing the same cap twice.
/// </para>
/// <para>
/// <b>Gateway refund.</b> Dispatched as a normal refund of the invoice first, so a refund is never counted twice: the refund
/// is the money, the note is the document. The note is recorded after the refund succeeded, under the refund's reference. A
/// refund that is refused creates no note. A refund made on its own, without this command, creates no note: nothing in the
/// panel requires one.
/// </para>
/// <para>
/// <b>Numbering.</b> The number comes from the <c>CN-</c> sequence of <see cref="INumberSequence"/> as the last step before the
/// save, after every check has passed, and a failed save is retried under the same number. The sequence is a database counter
/// that hands out a value for good, so it cannot promise there are no gaps: a process that stops between taking the number and
/// saving the note, or a request that is refused only after a retry found the invoice changed, leaves its number unused. An
/// operator must check what local law asks of the numbering.
/// </para>
/// </remarks>
public sealed class IssueCreditNoteHandler
{
    /// <summary>How many times the save is tried under one number before it gives up.</summary>
    private const int MaxAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Hands out the next credit note number.</summary>
    private readonly INumberSequence _numbers;

    /// <summary>Puts an account credit note on the client balance and writes the history line in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Dispatches the refund of a gateway refund note.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger; records a number that was taken and not used, and a refund that could not be recorded.</summary>
    private readonly ILogger<IssueCreditNoteHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="numbers">Hands out the next credit note number.</param>
    /// <param name="clientCredit">Puts an account credit note on the client balance in the same save.</param>
    /// <param name="bus">Dispatches the refund of a gateway refund note.</param>
    /// <param name="activity">Records the activity log line for this action.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public IssueCreditNoteHandler(
        IHostpanelDbContext context,
        INumberSequence numbers,
        IClientCredit clientCredit,
        IMessageBus bus,
        IActivityLogger activity,
        IClock clock,
        ILogger<IssueCreditNoteHandler> logger)
    {
        _context = context;
        _numbers = numbers;
        _clientCredit = clientCredit;
        _bus = bus;
        _activity = activity;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles <see cref="IssueCreditNoteCommand"/>.</summary>
    /// <param name="cmd">The credit note to issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the new note's id, or <c>INVOICE_NOT_FOUND</c>; <c>INVOICE_NOT_PAID</c> when the invoice is not Paid or
    /// PartiallyRefunded; <c>CREDIT_NOTE_TOP_UP_INVOICE</c> for a credit top-up; <c>CREDIT_NOTE_EXCEEDS_CREDITABLE</c> when the
    /// note is more than the invoice can still be credited; <c>INVOICE_REFUND_EXCEEDS_PAID</c> when a gateway refund is more
    /// than is still refundable; <c>CREDIT_NOTE_CURRENCY_MISMATCH</c> when account credit is asked for an invoice in a currency
    /// other than the one the client balance is kept in; <c>CLIENT_NOT_FOUND</c>; and whatever the refund answers for a gateway refund
    /// (<c>INVOICE_REFUND_CONFLICT</c>, <c>INVOICE_NOT_GATEWAY_PAID</c>, <c>GATEWAY_REFUND_REFUSED</c> and the rest). A note
    /// that lost the race for the invoice row surfaces as the concurrency conflict (409).
    /// </returns>
    public async Task<Result<int>> HandleAsync(IssueCreditNoteCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return cmd.Method == CreditNoteMethod.AccountCredit
            ? await IssueAsAccountCreditAsync(cmd, ct)
            : await IssueAsGatewayRefundAsync(cmd, ct);
    }

    /// <summary>Issues the note as account credit: note, invoice cap, balance and history line in one save.</summary>
    /// <param name="cmd">The credit note to issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new note's id, or the refusal.</returns>
    private async Task<Result<int>> IssueAsAccountCreditAsync(IssueCreditNoteCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        string? number = null;

        for (var attempt = 1; ; attempt++)
        {
            var invoice = await LoadInvoiceAsync(cmd.InvoiceId, ct);
            if (invoice is null)
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
            }

            CreditNote note;
            try
            {
                note = BuildNote(invoice, cmd, now);
                invoice.AddCreditNoted(note.Total);
            }
            catch (DomainRuleException) when (number is not null)
            {
                // A retry found the invoice changed under the first attempt, and the number it took is not used.
                _logger.LogWarning(
                    "Credit note number {Number} was taken for invoice {InvoiceId} and not used: the invoice changed before the save.",
                    number, invoice.Id);
                throw;
            }

            // The balance carries no currency of its own: it is kept in the client's billing currency, so a note in another
            // currency would be credited at the wrong value. Checked before a number is taken, so a refusal wastes none.
            if (!await _bus.IsClientBillingCurrencyAsync(invoice, invoice.Currency, ct))
            {
                _context.DetachAll();
                return Result<int>.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_CURRENCY_MISMATCH);
            }

            number ??= CreditNote.FormatNumber(await _numbers.NextAsync(NumberKind.CreditNote, ct));
            note.Issue(number, now);
            note.MarkApplied(now, refundReference: null);
            _context.CreditNotes.Add(note);

            var credited = await _clientCredit.AddAsync(
                new ClientCreditChange(
                    invoice.ClientId,
                    note.Total,
                    CreditMovementType.CreditNoteIssued,
                    invoice.Id,
                    Note: $"Credit note {number} issued against invoice #{invoice.Id}"),
                ct);
            if (credited.IsFailure)
            {
                _context.DetachAll();
                return Result<int>.FailureCode(credited.ErrorCode!);
            }

            await _activity.RecordAsync(ActivityEntry(note, number), ct);

            try
            {
                await _context.SaveChangesAsync(ct);
                return Result.Success(note.Id);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Another writer touched the invoice or the balance; read both again and judge the note on what is there now.
                _context.DetachAll();
            }
        }
    }

    /// <summary>
    /// Issues the note as a gateway refund: the refund first, as one ordinary refund of the invoice, then the note that records it.
    /// </summary>
    /// <param name="cmd">The credit note to issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new note's id, or the refusal.</returns>
    private async Task<Result<int>> IssueAsGatewayRefundAsync(IssueCreditNoteCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var invoice = await LoadInvoiceAsync(cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Checked before any money moves; the refund repeats the cash checks and holds the invoice while it runs.
        var note = BuildNote(invoice, cmd, now);
        var invoiceId = invoice.Id;

        var refunded = await _bus.InvokeAsync<Result>(
            new RefundInvoicePaymentCommand(invoiceId, note.Total, RefundType.Gateway, Notes: $"Credit note: {note.Reason}"), ct);
        if (refunded.IsFailure)
        {
            return Result<int>.FailureCode(refunded.ErrorCode!);
        }

        // The money has left. From here the note is recorded whatever the caller does, so it runs on no request token.
        _context.DetachAll();
        var reference = await _context.InvoiceTransactions
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.Type == InvoiceTransactionType.Refund)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Select(t => t.TransactionId)
            .FirstAsync(CancellationToken.None);

        var number = CreditNote.FormatNumber(await _numbers.NextAsync(NumberKind.CreditNote, CancellationToken.None));
        note.Issue(number, now);
        note.MarkApplied(now, reference);

        for (var attempt = 1; ; attempt++)
        {
            _context.CreditNotes.Add(note);
            await _activity.RecordAsync(ActivityEntry(note, number), CancellationToken.None);
            try
            {
                await _context.SaveChangesAsync(CancellationToken.None);
                return Result.Success(note.Id);
            }
            catch (Exception ex) when (ex is ConcurrencyConflictException or DbUpdateException)
            {
                if (attempt == MaxAttempts)
                {
                    _logger.LogCritical(
                        ex,
                        "Credit note {Number} for the gateway refund {Reference} of invoice {InvoiceId} could not be recorded; the money has been refunded.",
                        number, reference, invoiceId);
                    throw;
                }

                _logger.LogWarning(
                    ex, "Recording credit note {Number} of invoice {InvoiceId} failed on attempt {Attempt}; retrying.", number, invoiceId, attempt);
                _context.DetachAll();
            }
        }
    }

    /// <summary>Loads the invoice with the payments and refunds the caps are computed from.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked invoice, or null when none has the id.</returns>
    private Task<Invoice?> LoadInvoiceAsync(int invoiceId, CancellationToken ct) =>
        _context.Invoices.Include(i => i.Transactions).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    /// <summary>Builds the draft note from the command; the domain checks the invoice can take it.</summary>
    /// <param name="invoice">The invoice to credit.</param>
    /// <param name="cmd">The command.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>The draft note.</returns>
    private static CreditNote BuildNote(Invoice invoice, IssueCreditNoteCommand cmd, DateTimeOffset now)
    {
        IReadOnlyList<CreditNoteItem> items =
        [
            .. cmd.Items.Select(i => CreditNoteItem.Create(i.Description, i.UnitPrice, i.Quantity, invoice.Currency)),
        ];
        return CreditNote.Create(invoice, items, cmd.Method, cmd.Reason, now);
    }

    /// <summary>The activity log line for an issued note.</summary>
    /// <param name="note">The issued note.</param>
    /// <param name="number">Its number.</param>
    /// <returns>The entry, attached to the note's client.</returns>
    private static ActivityLogEntry ActivityEntry(CreditNote note, string number) =>
        new(note.ClientId, $"Issued Credit Note {number} - Invoice ID: {note.InvoiceId} - Amount: {note.Total}");
}
