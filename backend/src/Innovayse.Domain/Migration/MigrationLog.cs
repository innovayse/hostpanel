namespace Innovayse.Domain.Migration;

using Innovayse.Domain.Common;

/// <summary>
/// Records the outcome of a single record import attempt during a migration job.
/// One row per entity processed (imported, skipped, or failed).
/// </summary>
public sealed class MigrationLog : Entity
{
    /// <summary>The migration job this log entry belongs to.</summary>
    public int JobId { get; private set; }

    /// <summary>Type of entity that was processed.</summary>
    public MigrationEntityType EntityType { get; private set; }

    /// <summary>
    /// Human-readable identifier for the record (email address for clients/invoices/services/tickets,
    /// domain name for domains).
    /// </summary>
    public string Identifier { get; private set; } = string.Empty;

    /// <summary>What happened to this record during import.</summary>
    public MigrationLogAction Action { get; private set; }

    /// <summary>Explanation for Skipped or Failed actions; null for Imported.</summary>
    public string? Reason { get; private set; }

    /// <summary>When this log entry was written.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private MigrationLog() : base(0) { }

    /// <summary>Creates a new log entry for a processed migration record.</summary>
    public static MigrationLog Create(
        int jobId,
        MigrationEntityType entityType,
        string identifier,
        MigrationLogAction action,
        string? reason = null)
    {
        return new MigrationLog
        {
            JobId      = jobId,
            EntityType = entityType,
            Identifier = identifier,
            Action     = action,
            Reason     = reason,
            CreatedAt  = DateTimeOffset.UtcNow,
        };
    }
}
