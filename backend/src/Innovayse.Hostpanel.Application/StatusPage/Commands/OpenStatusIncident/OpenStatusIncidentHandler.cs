using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.OpenStatusIncident;

/// <summary>Announces a <see cref="StatusIncident"/> at the current moment and persists it.</summary>
public sealed class OpenStatusIncidentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the announcement moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the announcement moment is read from.</param>
    public OpenStatusIncidentHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The incident to announce.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new incident's id. This use case refuses nothing the validator lets through.</returns>
    public async Task<Result<int>> HandleAsync(OpenStatusIncidentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var incident = StatusIncident.Open(cmd.Title, cmd.Message, _clock.UtcNow);
        _context.StatusIncidents.Add(incident);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(incident.Id);
    }
}
