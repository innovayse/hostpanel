using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RejectBankTransfer;

/// <summary>
/// Rejects the manual bank transfer of an invoice: the money did not arrive. The invoice returns to <c>Unpaid</c>, so the
/// client can pay it again by any method, and the note is announced for the client's mail. A transfer that is no longer
/// waiting (confirmed or rejected a moment ago) is refused, so a confirmed payment can never be turned back by a late reject.
/// </summary>
public sealed class RejectBankTransferHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the moment of the rejection is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the moment of the rejection is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public RejectBankTransferHandler(IHostpanelDbContext context, IClock clock, IActivityLogger activity)
    {
        _context = context;
        _clock = clock;
        _activity = activity;
    }

    /// <summary>Handles <see cref="RejectBankTransferCommand"/>.</summary>
    /// <param name="cmd">The reject command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice; <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c> when no
    /// transfer waits on it.
    /// </returns>
    public async Task<Result> HandleAsync(RejectBankTransferCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsBankTransferPending)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        }

        invoice.RejectBankTransfer(cmd.Note.Trim(), _clock.UtcNow);

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Rejected Bank Transfer - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
