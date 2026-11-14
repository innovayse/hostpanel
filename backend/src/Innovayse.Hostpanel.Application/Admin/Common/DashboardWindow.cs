using Innovayse.Hostpanel.Application.Admin.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// The calendar days a dashboard period covers, cut in the host time zone, and the period of the same length
/// right before it. Every series of the dashboard is bucketed by the days here, so charts agree on where a day ends.
/// </summary>
public sealed class DashboardWindow
{
    /// <summary>The time zone whose midnight ends a day.</summary>
    private readonly TimeZoneInfo _zone;

    /// <summary>Initializes the window.</summary>
    /// <param name="zone">The time zone whose midnight ends a day.</param>
    /// <param name="days">Every day of the period, oldest first.</param>
    /// <param name="from">The first instant of the first day (UTC).</param>
    /// <param name="to">The first instant after the last day (UTC).</param>
    /// <param name="previousFrom">The first instant of the period right before this one (UTC).</param>
    private DashboardWindow(
        TimeZoneInfo zone,
        IReadOnlyList<DateOnly> days,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset previousFrom)
    {
        _zone = zone;
        Days = days;
        From = from;
        To = to;
        PreviousFrom = previousFrom;
    }

    /// <summary>Gets every day of the period, oldest first, today last.</summary>
    public IReadOnlyList<DateOnly> Days { get; }

    /// <summary>Gets the first instant of the first day (UTC), inclusive.</summary>
    public DateTimeOffset From { get; }

    /// <summary>Gets the first instant after the last day (UTC), exclusive.</summary>
    public DateTimeOffset To { get; }

    /// <summary>Gets the first instant of the period right before this one (UTC); that period ends at <see cref="From"/>.</summary>
    public DateTimeOffset PreviousFrom { get; }

    /// <summary>Builds the window that ends with the day <paramref name="now"/> falls in.</summary>
    /// <param name="period">How many days the window covers.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="zone">The time zone days are cut by.</param>
    /// <returns>The window.</returns>
    public static DashboardWindow Create(DashboardPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var length = (int)period;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var first = today.AddDays(1 - length);
        IReadOnlyList<DateOnly> days = [.. Enumerable.Range(0, length).Select(first.AddDays)];
        return new DashboardWindow(
            zone,
            days,
            StartOfDay(first, zone),
            StartOfDay(today.AddDays(1), zone),
            StartOfDay(first.AddDays(-length), zone));
    }

    /// <summary>Finds the calendar day a moment falls on in the window's time zone.</summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The day, which may lie outside the window.</returns>
    public DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, _zone).DateTime);

    /// <summary>Finds the first instant of a day in a time zone.</summary>
    /// <param name="day">The calendar day.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>Midnight of the day as a UTC instant; when the zone skips midnight that day, the first instant that exists.</returns>
    private static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        if (zone.IsInvalidTime(midnight))
        {
            // A zone that changes its clock at midnight has no such instant that day: the day starts when the clock resumes.
            midnight = midnight.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
    }
}
