using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusMaintenance;

/// <summary>Removes a maintenance window together with its list of affected components.</summary>
public sealed class DeleteStatusMaintenanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteStatusMaintenanceHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The window to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_MAINTENANCE_NOT_FOUND</c> when the id names no window.</returns>
    public async Task<Result> HandleAsync(DeleteStatusMaintenanceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entity = await _context.StatusMaintenances.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (entity is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_MAINTENANCE_NOT_FOUND);
        }

        _context.StatusMaintenances.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
