namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// The granularity a query groups moments by in the database before a calendar day is cut from them in code.
/// A day of the host time zone is not a UTC day, so the database cannot count days itself without loading every
/// row; it groups by UTC quarter hour instead, and the few buckets that come back are assigned to days. A quarter
/// hour is the finest step of every real zone offset and daylight saving change, so a bucket never straddles midnight.
/// </summary>
public static class TimeBucket
{
    /// <summary>Length of one bucket in minutes.</summary>
    public const int Minutes = 15;

    /// <summary>Builds the first instant of a bucket from the parts the database grouped by.</summary>
    /// <param name="year">The UTC year.</param>
    /// <param name="month">The UTC month.</param>
    /// <param name="day">The UTC day of the month.</param>
    /// <param name="hour">The UTC hour.</param>
    /// <param name="quarter">The index of the bucket inside the hour, the minute divided by <see cref="Minutes"/>.</param>
    /// <returns>The first instant of the bucket (UTC).</returns>
    public static DateTimeOffset StartOf(int year, int month, int day, int hour, int quarter) =>
        new(year, month, day, hour, quarter * Minutes, 0, TimeSpan.Zero);
}
