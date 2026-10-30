using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;

/// <summary>
/// Handles <see cref="ActivateDigitalServiceCommand"/>: turns a pending digital service Active and starts its
/// billing period from the activation moment, with no server, no panel call and no provisioning mail.
/// </summary>
public sealed class ActivateDigitalServiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the current moment, the billing anchor.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">Reads the current moment, the billing anchor.</param>
    public ActivateDigitalServiceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The service to activate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PENDING</c> when it is not Pending.</returns>
    public async Task<Result> HandleAsync(ActivateDigitalServiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is not ServiceStatus.Pending)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_PENDING);
        }

        service.ActivateDigital(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
