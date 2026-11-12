using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusMaintenance;

/// <summary>Changes the announcement of a maintenance window and persists it.</summary>
public sealed class UpdateStatusMaintenanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the change moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the change moment is read from.</param>
    public UpdateStatusMaintenanceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The window and its new announcement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>STATUS_MAINTENANCE_NOT_FOUND</c> when the id names no window, or <c>STATUS_COMPONENT_NOT_FOUND</c> when an
    /// affected component id names no component. A window that already ended or was cancelled is refused by the aggregate as
    /// <c>STATUS_MAINTENANCE_ALREADY_CLOSED</c>.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateStatusMaintenanceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var maintenance = await _context.StatusMaintenances.FirstOrDefaultAsync(m => m.Id == cmd.Id, ct);
        if (maintenance is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_MAINTENANCE_NOT_FOUND);
        }

        if (!await _context.AllStatusComponentsExistAsync(cmd.ComponentIds, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
        }

        maintenance.Update(cmd.Title, cmd.Description, cmd.ScheduledStart, cmd.ScheduledEnd, cmd.ComponentIds, _clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
