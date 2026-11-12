using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusIncident;

/// <summary>Removes an incident together with its timeline. A component the checks opened it for only loses the link.</summary>
public sealed class DeleteStatusIncidentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteStatusIncidentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The incident to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_INCIDENT_NOT_FOUND</c> when the id names no incident.</returns>
    public async Task<Result> HandleAsync(DeleteStatusIncidentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entity = await _context.StatusIncidents.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (entity is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
        }

        _context.StatusIncidents.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
