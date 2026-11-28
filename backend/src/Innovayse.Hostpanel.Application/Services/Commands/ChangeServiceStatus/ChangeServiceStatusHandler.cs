using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateService;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.ChangeServiceStatus;

/// <summary>
/// Handles <see cref="ChangeServiceStatusCommand"/>: checks the move against the service's status table,
/// then dispatches the one use case of the matching transition (<see cref="SuspendServiceCommand"/>,
/// <see cref="UnsuspendServiceCommand"/> or <see cref="TerminateServiceCommand"/>), so a status change
/// from the service page reaches the panel and fails exactly like the dedicated actions.
/// </summary>
public sealed class ChangeServiceStatusHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Dispatches the transition's own use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Dispatches the transition's own use case.</param>
    public ChangeServiceStatusHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Moves the service to the asked-for status.</summary>
    /// <param name="cmd">The service and the status asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also when the service already has that status), <c>SERVICE_NOT_FOUND</c> when the id
    /// names no service, the reason code of the status table (<see cref="Domain.Services.ClientService.StatusChangeRefusal"/>) when the service cannot be moved to
    /// that status, or the transition's own failure (a refusal of the panel) otherwise.
    /// </returns>
    public async Task<Result> HandleAsync(ChangeServiceStatusCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.StatusChangeRefusal(cmd.Status) is { } refusal)
        {
            return Result.FailureCode(refusal);
        }

        if (cmd.Status == service.Status)
        {
            return Result.Success();
        }

        // StatusChangeRefusal allows no other target, so the switch is exhaustive for every move it let through.
        object transition = cmd.Status switch
        {
            ServiceStatus.Suspended => new SuspendServiceCommand(service.Id),
            ServiceStatus.Active => new UnsuspendServiceCommand(service.Id),
            _ => new TerminateServiceCommand(service.Id),
        };

        return await _bus.InvokeAsync<Result>(transition, ct);
    }
}
