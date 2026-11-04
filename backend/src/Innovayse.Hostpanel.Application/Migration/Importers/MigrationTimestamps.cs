namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Shapes a source timestamp the way the database keeps it, so an importer can match a record on a date it
/// has already stored: a retried run must compare equal to what the first run wrote.
/// </summary>
public static class MigrationTimestamps
{
    /// <summary>Ticks in one microsecond, the finest unit the database keeps.</summary>
    private const long TicksPerMicrosecond = 10;

    /// <summary>Drops the sub-microsecond part of a timestamp, because the database stores microseconds and would never compare a finer tick equal.</summary>
    /// <param name="value">The timestamp read from the source.</param>
    /// <returns>The same instant cut down to whole microseconds.</returns>
    public static DateTimeOffset ToMicroseconds(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TicksPerMicrosecond));
}
