using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusMaintenance;

/// <summary>Announces a <see cref="StatusMaintenance"/> window and persists it.</summary>
public sealed class CreateStatusMaintenanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateStatusMaintenanceHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The window to announce.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new window's id, or <c>STATUS_COMPONENT_NOT_FOUND</c> when an affected component id names no component.</returns>
    public async Task<Result<int>> HandleAsync(CreateStatusMaintenanceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!await _context.AllStatusComponentsExistAsync(cmd.ComponentIds, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
        }

        var maintenance = StatusMaintenance.Schedule(cmd.Title, cmd.Description, cmd.ScheduledStart, cmd.ScheduledEnd, cmd.ComponentIds);
        _context.StatusMaintenances.Add(maintenance);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(maintenance.Id);
    }
}
