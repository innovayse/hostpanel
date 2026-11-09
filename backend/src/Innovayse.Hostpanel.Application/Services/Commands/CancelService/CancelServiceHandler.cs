using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateService;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.CancelService;

/// <summary>
/// Records a cancellation request for a client service and acts on it: an immediate one terminates
/// the service now, an end-of-period one marks it to be terminated when its current cycle ends.
/// </summary>
/// <remarks>
/// The request stays open until the service is terminated; the termination's event handler closes it.
/// </remarks>
public sealed class CancelServiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to terminate through the one terminate use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to terminate through the one terminate use case.</param>
    public CancelServiceHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The service to cancel, the cancellation type and an optional reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service,
    /// <c>SERVICE_ALREADY_TERMINATED</c> when it has already ended,
    /// <c>CANCELLATION_ALREADY_PENDING</c> when an open request already exists for it, or the
    /// terminate use case's failure for an immediate cancellation.
    /// </returns>
    public async Task<Result> HandleAsync(CancelServiceCommand cmd, CancellationToken ct)
    {
        // Tracked, not AsNoTracking: an end-of-period request changes the service's automation below.
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is ServiceStatus.Terminated)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_ALREADY_TERMINATED);
        }

        // Only an open request blocks a new one; a service may carry any number of closed ones.
        var hasOpenRequest = await _context.CancellationRequests
            .AnyAsync(r => r.ServiceId == cmd.ServiceId && r.Status == CancellationStatus.Open, ct);
        if (hasOpenRequest)
        {
            return Result.FailureCode(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
        }

        _context.CancellationRequests.Add(CancellationRequest.Create(service.Id, cmd.Type, cmd.Reason));
        if (cmd.Type is CancellationType.EndOfBillingPeriod)
        {
            // The suspension hold is the staff's setting and stays as it is; only the end of the
            // service is added.
            service.ConfigureAutomation(service.AutoRenew, service.OverrideAutoSuspend, service.SuspendUntil, autoTerminateEndOfCycle: true, cmd.Reason);
        }

        // Saved before an immediate termination, so the request exists for the termination's event
        // handler to close, and stays open for staff when the termination fails.
        await _context.SaveChangesAsync(ct);

        if (cmd.Type is CancellationType.Immediate)
        {
            return await _bus.InvokeAsync<Result>(new TerminateServiceCommand(service.Id), ct);
        }

        return Result.Success();
    }
}
