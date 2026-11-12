using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusOverview;

/// <summary>
/// Loads the groups with their components and the incidents that are not resolved yet, and works out the
/// overall state from the components. A component a maintenance window that is under way affects shows as under
/// maintenance, unless an operator forced a state onto it.
/// </summary>
public sealed class GetStatusOverviewHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the maintenance windows are judged by.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the maintenance windows are judged by.</param>
    public GetStatusOverviewHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The overview. Never refuses.</returns>
    public async Task<Result<StatusOverviewDto>> HandleAsync(GetStatusOverviewQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var groups = await _context.StatusComponentGroups
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);

        var incidents = await _context.StatusIncidents
            .AsNoTracking()
            .Where(i => i.State != StatusIncidentState.Resolved)
            .OrderByDescending(i => i.StartedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync(ct);

        var underMaintenance = await _context.StatusComponentIdsUnderMaintenanceAsync(_clock.UtcNow, ct);
        var groupDtos = groups.Select(g => g.ToDto(underMaintenance)).ToList();
        var overall = groupDtos
            .SelectMany(g => g.Components)
            .Select(c => c.State)
            .DefaultIfEmpty(StatusComponentState.Operational)
            .Max();

        return Result<StatusOverviewDto>.Success(new StatusOverviewDto(overall, groupDtos, incidents.Select(i => i.ToDto()).ToList()));
    }
}
