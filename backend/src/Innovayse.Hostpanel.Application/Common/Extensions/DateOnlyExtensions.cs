namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>
/// Turns the calendar days a list or a report is filtered by into UTC instants. A day range
/// <c>[from, to]</c> is always queried as the half-open window <c>[from 00:00, to + 1 day 00:00)</c>,
/// so nothing in the last second of a day falls between two windows and two adjacent ranges never
/// count the same row twice.
/// </summary>
public static class DateOnlyExtensions
{
    /// <summary>The first instant of the day, UTC.</summary>
    /// <param name="day">The calendar day.</param>
    /// <returns>Midnight at the start of <paramref name="day"/>, UTC.</returns>
    public static DateTimeOffset StartOfDayUtc(this DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>The exclusive upper bound of a range that ends on the day: midnight at the start of the next day, UTC.</summary>
    /// <param name="day">The last calendar day the range includes.</param>
    /// <returns>Midnight at the start of the day after <paramref name="day"/>, UTC.</returns>
    public static DateTimeOffset EndOfDayExclusiveUtc(this DateOnly day) => day.AddDays(1).StartOfDayUtc();
}
