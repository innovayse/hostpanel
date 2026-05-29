namespace Innovayse.Domain.Notifications.Interfaces;

/// <summary>Persistence contract for <see cref="EmailLog"/> entities.</summary>
public interface IEmailLogRepository
{
    /// <summary>Stages a new email log entry for insertion.</summary>
    /// <param name="log">The log entry to add.</param>
    void Add(EmailLog log);

    /// <summary>Returns a paged list of email log entries, ordered by <see cref="EmailLog.SentAt"/> descending.</summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of entries per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of log entries for the requested page.</returns>
    Task<IReadOnlyList<EmailLog>> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a paged list of email log entries sent to a specific client, ordered by <see cref="EmailLog.SentAt"/> descending.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of entries per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the requested page and total matching count.</returns>
    Task<(IReadOnlyList<EmailLog> Items, int TotalCount)> ListByClientIdAsync(int clientId, int page, int pageSize, CancellationToken ct);
}
