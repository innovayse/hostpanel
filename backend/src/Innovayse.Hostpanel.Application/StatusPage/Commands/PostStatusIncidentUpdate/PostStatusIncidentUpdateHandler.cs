using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.PostStatusIncidentUpdate;

/// <summary>Adds a timeline entry to an incident and persists it.</summary>
public sealed class PostStatusIncidentUpdateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the posting moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the posting moment is read from.</param>
    public PostStatusIncidentUpdateHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The incident, the state it moves to and the message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>STATUS_INCIDENT_NOT_FOUND</c> when the id names no incident. An incident that is already resolved
    /// is refused by the aggregate as <c>STATUS_INCIDENT_ALREADY_RESOLVED</c>.
    /// </returns>
    public async Task<Result> HandleAsync(PostStatusIncidentUpdateCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var incident = await _context.StatusIncidents.FirstOrDefaultAsync(i => i.Id == cmd.Id, ct);
        if (incident is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
        }

        incident.PostUpdate(cmd.State, cmd.Message, _clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
