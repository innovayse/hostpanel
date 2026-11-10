using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusIncidents;

/// <summary>Loads the incidents announced within the requested window, with their timelines.</summary>
public sealed class ListStatusIncidentsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the window is counted back from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the window is counted back from.</param>
    public ListStatusIncidentsHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The window to look back over.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The incidents announced within the window, newest first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<StatusIncidentDto>>> HandleAsync(ListStatusIncidentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var since = _clock.UtcNow.AddDays(-query.Days);
        var incidents = await _context.StatusIncidents
            .AsNoTracking()
            .Where(i => i.StartedAt >= since)
            .OrderByDescending(i => i.StartedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<StatusIncidentDto>>.Success(incidents.Select(i => i.ToDto()).ToList());
    }
}
