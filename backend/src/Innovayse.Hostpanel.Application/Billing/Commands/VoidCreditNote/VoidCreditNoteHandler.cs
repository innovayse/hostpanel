using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.VoidCreditNote;

/// <summary>
/// Voids a credit note. A note kept as account credit gives its value back: it comes off the client balance and off the
/// invoice's credit noted amount in the same save, so the invoice can be credited or refunded again. A note paid out as a
/// gateway refund cannot be voided, because the refund cannot be taken back.
/// </summary>
public sealed class VoidCreditNoteHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Takes the value off the client balance and writes the history line in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Takes the value off the client balance in the same save.</param>
    /// <param name="activity">Records the activity log line for this action.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public VoidCreditNoteHandler(IHostpanelDbContext context, IClientCredit clientCredit, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _clientCredit = clientCredit;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Handles <see cref="VoidCreditNoteCommand"/>.</summary>
    /// <param name="cmd">The void.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CREDIT_NOTE_NOT_FOUND</c>; <c>CREDIT_NOTE_ALREADY_VOIDED</c>; <c>CREDIT_NOTE_REFUND_NOT_VOIDABLE</c> for a
    /// note paid out as a gateway refund; <c>CLIENT_CREDIT_INSUFFICIENT</c> when the client has already spent the credit the
    /// note gave. Nothing is saved on a refusal.
    /// </returns>
    public async Task<Result> HandleAsync(VoidCreditNoteCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var note = await _context.CreditNotes.FirstOrDefaultAsync(n => n.Id == cmd.CreditNoteId, ct);
        if (note is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }

        var givenCredit = note.Method == CreditNoteMethod.AccountCredit && note.Status == CreditNoteStatus.Applied;
        var heldOnInvoice = note.HoldsInvoiceCredit;

        // Throws for a note already voided, a draft, or a refund that cannot come back; the middleware answers it.
        note.Void(cmd.Reason, _clock.UtcNow);

        if (heldOnInvoice)
        {
            var invoice = await _context.Invoices.FirstAsync(i => i.Id == note.InvoiceId, ct);
            invoice.RemoveCreditNoted(note.Total);
        }

        if (givenCredit)
        {
            var taken = await _clientCredit.DeductAsync(
                new ClientCreditChange(
                    note.ClientId,
                    note.Total,
                    CreditMovementType.CreditNoteVoided,
                    note.InvoiceId,
                    Note: $"Credit note {note.Number} voided"),
                ct);
            if (taken.IsFailure)
            {
                // The client spent the credit the note gave; nothing is staged for saving, so nothing changes.
                _context.DetachAll();
                return Result.FailureCode(taken.ErrorCode!);
            }
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(note.ClientId, $"Voided Credit Note {note.Number} - Invoice ID: {note.InvoiceId} - Amount: {note.Total}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
