namespace Innovayse.Domain.Audit.Interfaces;

/// <summary>Persistence contract for <see cref="ActivityLog"/> entries.</summary>
public interface IActivityLogRepository
{
    /// <summary>Stages a new activity log entry for insertion.</summary>
    /// <param name="log">The log entry to add.</param>
    void Add(ActivityLog log);

    /// <summary>
    /// Returns a paged list of activity log entries for a specific client, newest first.
    /// Supports optional filters on date, admin name/email, description, and IP address.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of entries per page.</param>
    /// <param name="date">Optional date filter — matches entries on this UTC day.</param>
    /// <param name="adminSearch">Optional partial match on admin name or email.</param>
    /// <param name="description">Optional partial match on description.</param>
    /// <param name="ipAddress">Optional partial match on IP address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of matching items and total count.</returns>
    Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize,
        DateTimeOffset? date, string? adminSearch, string? description, string? ipAddress,
        CancellationToken ct);
}
