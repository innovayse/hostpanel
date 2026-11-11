using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.RejectServiceChangeRequest;

/// <summary>
/// Turns a pending or approved plan change request down. An approved upgrade still waiting for its proration invoice to be
/// paid is rejected too, and the unpaid invoice is cancelled with it, so the client is not left owing for a change that
/// will not happen.
/// </summary>
/// <remarks>
/// The invoice is cancelled before the request is saved, and an invoice that is already cancelled is left alone, so a
/// reject that failed halfway can be repeated. An invoice that is paid cannot be cancelled and refuses the reject: the
/// client has paid for the change, and staff apply it (approve) rather than turn it down.
/// </remarks>
public sealed class RejectServiceChangeRequestHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to read and cancel the proration invoice through the Billing module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the moment of the rejection is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to read and cancel the proration invoice through the Billing module.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="clock">The clock the moment of the rejection is read from.</param>
    public RejectServiceChangeRequestHandler(IHostpanelDbContext context, IMessageBus bus, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _bus = bus;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The request to reject and an optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>SERVICE_CHANGE_REQUEST_NOT_FOUND</c> when the id names no request, <c>SERVICE_CHANGE_INVALID_STATE</c>
    /// when it is already Completed or Rejected, or the Billing module's refusal to cancel a paid invoice.
    /// </returns>
    public async Task<Result> HandleAsync(RejectServiceChangeRequestCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var request = await _context.ServiceChangeRequests.FirstOrDefaultAsync(r => r.Id == cmd.RequestId, ct);
        if (request is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_REQUEST_NOT_FOUND);
        }

        if (!request.IsOpen)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        }

        if (request.InvoiceId is { } invoiceId)
        {
            var cancelled = await CancelInvoiceAsync(invoiceId, ct);
            if (cancelled.IsFailure)
            {
                return cancelled;
            }
        }

        request.Reject(cmd.Note, _clock.UtcNow);
        await _activity.RecordAsync(
            new ActivityLogEntry(request.ClientId, $"Rejected {request.Type} - Service ID: {request.ServiceId}, Request ID: {request.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>Cancels the proration invoice unless it is cancelled already.</summary>
    /// <param name="invoiceId">The invoice the request waits for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the Billing module's refusal, for example for an invoice that is paid.</returns>
    private async Task<Result> CancelInvoiceAsync(int invoiceId, CancellationToken ct)
    {
        var brief = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct);
        if (brief.Value is { Status: InvoiceStatus.Cancelled })
        {
            return Result.Success();
        }

        return await _bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoiceId), ct);
    }
}
