using Innovayse.Hostpanel.Application.Admin.Common;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Extensions;

/// <summary>Turns moments into the per-day series the dashboard charts draw.</summary>
public static class DashboardSeriesExtensions
{
    /// <summary>Counts moments per day of the window, every day present.</summary>
    /// <param name="moments">The moments to count; the ones outside the window are ignored.</param>
    /// <param name="window">The days to count them on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One count per day of the window, oldest first, zero for a day with no moment.</returns>
    public static async Task<IReadOnlyList<DashboardDailyCountDto>> CountPerDayAsync(
        this IQueryable<DateTimeOffset> moments,
        DashboardWindow window,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(moments);
        ArgumentNullException.ThrowIfNull(window);

        // The database only narrows to the window: a calendar day of the host zone is not a UTC day, so the
        // bucketing happens here, on the bare timestamps.
        var inWindow = await moments.Where(m => m >= window.From && m < window.To).ToListAsync(ct);
        var counts = inWindow.GroupBy(window.DayOf).ToDictionary(g => g.Key, g => g.Count());
        return [.. window.Days.Select(day => new DashboardDailyCountDto(day, counts.GetValueOrDefault(day)))];
    }
}
