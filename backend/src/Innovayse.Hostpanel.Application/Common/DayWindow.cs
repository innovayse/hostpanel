namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// A run of calendar days cut in the host time zone, the one place that says where a day starts and ends. Every
/// per-day series (the dashboard charts and the daily performance report) is bucketed by the days here, so they
/// agree with each other on where a day ends.
/// </summary>
public sealed class DayWindow
{
    /// <summary>The time zone whose midnight ends a day.</summary>
    private readonly TimeZoneInfo _zone;

    /// <summary>Initializes the window.</summary>
    /// <param name="zone">The time zone whose midnight ends a day.</param>
    /// <param name="days">Every day of the window, oldest first.</param>
    /// <param name="from">The first instant of the first day (UTC).</param>
    /// <param name="to">The first instant after the last day (UTC).</param>
    private DayWindow(TimeZoneInfo zone, IReadOnlyList<DateOnly> days, DateTimeOffset from, DateTimeOffset to)
    {
        _zone = zone;
        Days = days;
        From = from;
        To = to;
    }

    /// <summary>Gets every day of the window, oldest first.</summary>
    public IReadOnlyList<DateOnly> Days { get; }

    /// <summary>Gets the first day of the window.</summary>
    public DateOnly First => Days[0];

    /// <summary>Gets the last day of the window.</summary>
    public DateOnly Last => Days[^1];

    /// <summary>Gets the first instant of the first day (UTC), inclusive.</summary>
    public DateTimeOffset From { get; }

    /// <summary>Gets the first instant after the last day (UTC), exclusive.</summary>
    public DateTimeOffset To { get; }

    /// <summary>Builds the window of the days from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
    /// <param name="first">The first day.</param>
    /// <param name="last">The last day, not before <paramref name="first"/>.</param>
    /// <param name="zone">The time zone days are cut by.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="last"/> is before <paramref name="first"/>.</exception>
    public static DayWindow Create(DateOnly first, DateOnly last, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (last < first)
        {
            throw new ArgumentException("A window ends on or after the day it starts.", nameof(last));
        }

        IReadOnlyList<DateOnly> days = [.. Enumerable.Range(0, last.DayNumber - first.DayNumber + 1).Select(first.AddDays)];
        return new DayWindow(zone, days, StartOfDay(first, zone), StartOfDay(last.AddDays(1), zone));
    }

    /// <summary>Finds the calendar day a moment falls on in a time zone.</summary>
    /// <param name="moment">The moment.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The day.</returns>
    public static DateOnly DayIn(DateTimeOffset moment, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);
    }

    /// <summary>Finds the calendar day a moment falls on in the window's time zone.</summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The day, which may lie outside the window.</returns>
    public DateOnly DayOf(DateTimeOffset moment) => DayIn(moment, _zone);

    /// <summary>Finds the first instant of a day in a time zone.</summary>
    /// <param name="day">The calendar day.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The instant the day starts at, UTC.</returns>
    private static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);

        if (zone.IsInvalidTime(midnight))
        {
            // The clock jumps over midnight, so the day starts at the jump: the instant the clock read midnight on
            // the offset it had just before, whatever the length of the jump.
            var before = zone.GetUtcOffset(midnight.AddTicks(-1));
            return new DateTimeOffset(midnight - before, TimeSpan.Zero);
        }

        if (zone.IsAmbiguousTime(midnight))
        {
            // The clock steps back over midnight, so midnight happens twice and the day starts the first time:
            // the later offset reaches the earlier instant.
            return new DateTimeOffset(midnight - zone.GetAmbiguousTimeOffsets(midnight).Max(), TimeSpan.Zero);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
    }
}
