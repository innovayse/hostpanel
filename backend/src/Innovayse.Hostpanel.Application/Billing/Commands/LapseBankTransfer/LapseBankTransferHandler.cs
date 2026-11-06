using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.LapseBankTransfer;

/// <summary>
/// Ends the wait of a manual bank transfer nobody confirmed or rejected: the invoice returns to open (Overdue when its due
/// date has passed), so the overdue and suspension flow that a waiting transfer pauses runs again, and the client is told
/// through the rejection mail. A transfer that is no longer waiting (confirmed a moment ago) is refused and left alone.
/// </summary>
public sealed class LapseBankTransferHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the moment of the lapse is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the moment of the lapse is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public LapseBankTransferHandler(IHostpanelDbContext context, IClock clock, IActivityLogger activity)
    {
        _context = context;
        _clock = clock;
        _activity = activity;
    }

    /// <summary>Handles <see cref="LapseBankTransferCommand"/>.</summary>
    /// <param name="cmd">The lapse command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice; <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c> when no
    /// transfer waits on it.
    /// </returns>
    public async Task<Result> HandleAsync(LapseBankTransferCommand cmd, CancellationToken ct)
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

        invoice.LapseBankTransfer(_clock.UtcNow);

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Bank Transfer Lapsed - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
