using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>Counts moments per calendar day of a <see cref="DayWindow"/>, the one per-day count every report shares.</summary>
public static class DayWindowExtensions
{
    /// <summary>Counts the moments inside the window per calendar day of the window's time zone.</summary>
    /// <param name="moments">The moments to count, already narrowed to what should count; the window's own bounds are applied here.</param>
    /// <param name="window">The days to count them on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of each day that has at least one moment; a quiet day has no entry.</returns>
    public static async Task<IReadOnlyDictionary<DateOnly, int>> CountPerDayAsync(
        this IQueryable<DateTimeOffset> moments,
        DayWindow window,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(moments);
        ArgumentNullException.ThrowIfNull(window);

        // The database groups by UTC bucket, so only a few rows come back whatever the table holds; the days of
        // the host zone are cut from those buckets here (see TimeBucket).
        var buckets = await moments
            .Where(m => m >= window.From && m < window.To)
            .GroupBy(m => new { m.Year, m.Month, m.Day, m.Hour, Quarter = m.Minute / TimeBucket.Minutes })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Quarter, Count = g.Count() })
            .ToListAsync(ct);

        return buckets
            .GroupBy(b => window.DayOf(TimeBucket.StartOf(b.Year, b.Month, b.Day, b.Hour, b.Quarter)))
            .ToDictionary(day => day.Key, day => day.Sum(b => b.Count));
    }
}
