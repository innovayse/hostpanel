namespace Innovayse.Domain.Migration.Interfaces;

/// <summary>Repository contract for <see cref="MigrationLog"/> persistence.</summary>
public interface IMigrationLogRepository
{
    /// <summary>Adds a log entry (does not save immediately).</summary>
    void Add(MigrationLog log);

    /// <summary>Persists pending log entries.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated list of log entries for a given job,
    /// optionally filtered by action and entity type.
    /// </summary>
    Task<(IReadOnlyList<MigrationLog> Items, int TotalCount)> ListByJobAsync(
        int jobId,
        MigrationLogAction? action,
        MigrationEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
