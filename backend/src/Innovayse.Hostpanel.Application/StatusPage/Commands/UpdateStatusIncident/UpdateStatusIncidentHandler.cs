using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusIncident;

/// <summary>Changes the headline of an incident and persists it, whether the incident is open or resolved.</summary>
public sealed class UpdateStatusIncidentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateStatusIncidentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The incident and its new headline.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_INCIDENT_NOT_FOUND</c> when the id names no incident.</returns>
    public async Task<Result> HandleAsync(UpdateStatusIncidentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var incident = await _context.StatusIncidents.FirstOrDefaultAsync(i => i.Id == cmd.Id, ct);
        if (incident is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
        }

        incident.Retitle(cmd.Title);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
