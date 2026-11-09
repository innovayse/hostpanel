using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.TerminateService;

/// <summary>
/// Removes a client service's hosting account from its panel, then marks the service terminated on the platform.
/// </summary>
/// <remarks>
/// The state is checked before the panel is asked, so a service the transition would refuse never
/// reaches the panel. The panel is asked synchronously and its refusal answered, so the platform's
/// record only changes when the panel agreed; a service with no account or no usable server is still
/// moved, because there is nowhere upstream to tell.
/// </remarks>
public sealed class TerminateServiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Sends the operation to the panel the account lives on.</summary>
    private readonly IHostedAccountOperations _hostedAccounts;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="hostedAccounts">Sends the operation to the panel the account lives on.</param>
    /// <param name="clock">Reads the current moment.</param>
    public TerminateServiceHandler(IHostpanelDbContext context, IHostedAccountOperations hostedAccounts, IClock clock)
    {
        _context = context;
        _hostedAccounts = hostedAccounts;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The service to terminate and the reason for the panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_TERMINATE_NOT_ALLOWED</c> when
    /// the service is already terminated, or <c>SERVICE_TERMINATE_UPSTREAM_FAILED</c> when the panel refuses.
    /// </returns>
    public async Task<Result> HandleAsync(TerminateServiceCommand cmd, CancellationToken ct)
    {
        // Tracked, not AsNoTracking: the service is mutated and saved below.
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (!service.CanTerminate)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_TERMINATE_NOT_ALLOWED);
        }

        var outcome = await _hostedAccounts.TerminateAsync(
            new HostedAccountTermination(HostedAccountTarget.Of(service), cmd.Reason ?? string.Empty), ct);
        if (outcome is HostedAccountOutcome.Failed)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_TERMINATE_UPSTREAM_FAILED);
        }

        // The panel was asked above and its failure answered, so the event that would ask again is skipped.
        service.Terminate(_clock.UtcNow, cmd.Reason ?? string.Empty, hostingAlreadyUpdated: true);

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
