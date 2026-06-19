namespace Innovayse.Domain.Migration;

/// <summary>Outcome of a single record processed during a migration job.</summary>
public enum MigrationLogAction
{
    /// <summary>The record was successfully created in the database.</summary>
    Imported,

    /// <summary>The record was intentionally skipped (e.g. duplicate email or domain).</summary>
    Skipped,

    /// <summary>An unexpected error occurred; the record was not imported.</summary>
    Failed,
}
