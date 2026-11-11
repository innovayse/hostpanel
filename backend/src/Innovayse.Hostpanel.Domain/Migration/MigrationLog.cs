using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Migration;

/// <summary>
/// Records the outcome of a single record import attempt during a migration job.
/// One row per entity processed (imported, skipped, or failed).
/// </summary>
public sealed class MigrationLog : BaseEntity
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

    /// <summary>Why the record was skipped or failed; null when it was imported.</summary>
    public MigrationSkipReason? SkipReason { get; private set; }

    /// <summary>
    /// Free-text failure detail kept only on rows written before failures were logged as
    /// <see cref="MigrationSkipReason.ImportFailed"/>; new rows never set it, so no exception text reaches admins.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>When this log entry was written.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Required by EF Core; use <see cref="Create"/>.</summary>
    private MigrationLog() { }

    /// <summary>Creates a new log entry for a processed migration record.</summary>
    /// <param name="jobId">Id of the job the record was processed for.</param>
    /// <param name="entityType">Kind of record.</param>
    /// <param name="identifier">Identifier of the record in the source system.</param>
    /// <param name="action">What happened to the record.</param>
    /// <param name="skipReason">Why it was skipped or failed, or null when it was imported.</param>
    /// <returns>The new log entry.</returns>
    public static MigrationLog Create(
        int jobId,
        MigrationEntityType entityType,
        string identifier,
        MigrationLogAction action,
        MigrationSkipReason? skipReason = null)
    {
        return new MigrationLog
        {
            JobId = jobId,
            EntityType = entityType,
            Identifier = identifier,
            Action = action,
            SkipReason = skipReason,
        };
    }
}
