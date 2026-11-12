using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RejectServiceChangeRequest;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// Frees the service when the proration invoice of an approved upgrade is cancelled: dispatches
/// <see cref="RejectServiceChangeRequestCommand"/> for the approved request that waits for the invoice, so the request does
/// not stay Approved for an invoice that can never be paid and block every later plan change of the service. A
/// non-notification handler of <see cref="InvoiceCancelledEvent"/>. It is idempotent: a request that is no longer Approved
/// (staff rejected it, or it was completed) is not found, and rejecting leaves an already cancelled invoice alone.
/// </summary>
public sealed class ServiceChangeInvoiceCancelledHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Dispatches the command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a refusal staff can settle by hand.</summary>
    private readonly ILogger<ServiceChangeInvoiceCancelledHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Dispatches the command.</param>
    /// <param name="logger">Records a refusal staff can settle by hand.</param>
    public ServiceChangeInvoiceCancelledHandler(
        IHostpanelDbContext context, IMessageBus bus, ILogger<ServiceChangeInvoiceCancelledHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Rejects the plan change that waits for the cancelled invoice, if there is one.</summary>
    /// <param name="evt">The cancellation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task. A failure to reach the database throws, so the durable queue retries the event.</returns>
    public async Task HandleAsync(InvoiceCancelledEvent evt, CancellationToken ct)
    {
        var requestId = await _context.ServiceChangeRequests
            .AsNoTracking()
            .Where(r => r.InvoiceId == evt.InvoiceId && r.Status == ServiceChangeStatus.Approved)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (requestId is null)
        {
            return;
        }

        var outcome = await _bus.InvokeAsync<Result>(new RejectServiceChangeRequestCommand(requestId.Value, null), ct);
        if (outcome.IsFailure)
        {
            _logger.LogWarning(
                "Rejecting the plan change of cancelled invoice {InvoiceId} was refused with {ErrorCode}; staff can reject it.",
                evt.InvoiceId, outcome.ErrorCode);
        }
    }
}
