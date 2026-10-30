using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RetryFulfilmentRecord;

/// <summary>
/// Handles <see cref="RetryFulfilmentRecordCommand"/>: resets a failed record through
/// <see cref="Domain.Billing.FulfilmentRecord.Retry"/> and kicks one dispatch pass, so the plugin hears
/// about it in seconds instead of at the next beat.
/// </summary>
public sealed class RetryFulfilmentRecordHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The current moment (UTC); the retried record is due from it.</summary>
    private readonly IClock _clock;

    /// <summary>Starts the extra dispatch pass once the reset is saved.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="clock">The current moment (UTC); the retried record is due from it.</param>
    /// <param name="bus">Starts the extra dispatch pass once the reset is saved.</param>
    public RetryFulfilmentRecordHandler(
        IHostpanelDbContext context, IActivityLogger activity, IClock clock, IMessageBus bus)
    {
        _context = context;
        _activity = activity;
        _clock = clock;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The retry command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>FULFILMENT_RECORD_NOT_FOUND</c> when the invoice has no such record, or
    /// <c>FULFILMENT_RECORD_NOT_RETRYABLE</c> when the record has not failed.
    /// </returns>
    public async Task<Result> HandleAsync(RetryFulfilmentRecordCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Matched on both ids so a record id of another invoice is not found here.
        var record = await _context.FulfilmentRecords
            .FirstOrDefaultAsync(r => r.Id == cmd.RecordId && r.InvoiceId == cmd.InvoiceId, ct);
        if (record is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_FOUND);
        }

        var clientId = await _context.Invoices
            .Where(i => i.Id == cmd.InvoiceId)
            .Select(i => (int?)i.ClientId)
            .FirstOrDefaultAsync(ct);

        record.Retry(_clock.UtcNow);
        await _activity.RecordAsync(
            new ActivityLogEntry(clientId, $"Retried Fulfilment - Invoice ID: {cmd.InvoiceId}, Record ID: {record.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        // Best effort, as for a payment: the record is saved and due, so the scheduled chain delivers it
        // if this pass never runs.
        await _bus.PublishAsync(new DispatchFulfilmentNowCommand());
        return Result.Success();
    }
}
