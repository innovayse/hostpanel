using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusUptime;

/// <summary>
/// Counts the checks of the requested window per component and per UTC day, and turns the counts into uptime
/// percentages: the share of checks that succeeded. Every failed check counts as downtime, including the failures
/// below the component's failure threshold that have not opened an incident yet.
/// </summary>
public sealed class GetStatusUptimeHandler
{
    /// <summary>Decimals the percentages are rounded to, so 99.965 stays distinguishable from 99.97.</summary>
    private const int PercentDecimals = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the window is counted back from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the window is counted back from.</param>
    public GetStatusUptimeHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The window to report over.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The uptime of every component. A component with no check in the window has no percentage. Never refuses.</returns>
    public async Task<Result<StatusUptimeDto>> HandleAsync(GetStatusUptimeQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var days = (int)query.Days;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var firstDay = today.AddDays(1 - days);
        var since = new DateTimeOffset(firstDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Grouped in the database: a window holds a check every few minutes per component, far too many rows to load.
        var counts = await _context.StatusChecks
            .AsNoTracking()
            .Where(c => c.CheckedAt >= since)
            .GroupBy(c => new { c.ComponentId, DayIndex = (c.CheckedAt - since).Days })
            .Select(g => new { g.Key.ComponentId, g.Key.DayIndex, Total = g.Count(), Succeeded = g.Count(c => c.Succeeded) })
            .ToListAsync(ct);
        var byComponent = counts.ToLookup(c => c.ComponentId);

        var groups = await _context.StatusComponentGroups
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);

        var components = groups
            .SelectMany(g => g.Components.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id))
            .Select(component =>
            {
                var perDay = byComponent[component.Id].ToDictionary(c => firstDay.AddDays(c.DayIndex));
                var daily = Enumerable.Range(0, days)
                    .Select(offset => firstDay.AddDays(offset))
                    .Select(day => new StatusUptimeDayDto(
                        day,
                        perDay.TryGetValue(day, out var count) ? Percent(count.Succeeded, count.Total) : null))
                    .ToList();
                var overall = Percent(perDay.Values.Sum(c => c.Succeeded), perDay.Values.Sum(c => c.Total));
                return new StatusComponentUptimeDto(component.Id, component.Name, overall, daily);
            })
            .ToList();

        return Result<StatusUptimeDto>.Success(new StatusUptimeDto(days, components));
    }

    /// <summary>Works out the share of checks that succeeded.</summary>
    /// <param name="succeeded">How many checks succeeded.</param>
    /// <param name="total">How many checks ran.</param>
    /// <returns>The percentage, 0 to 100, or <c>null</c> when no check ran.</returns>
    private static double? Percent(int succeeded, int total)
    {
        return total == 0 ? null : Math.Round(succeeded * 100d / total, PercentDecimals);
    }
}
