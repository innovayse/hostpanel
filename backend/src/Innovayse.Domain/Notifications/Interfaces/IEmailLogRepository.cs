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

    /// <summary>Returns one email log entry, but only if it was sent to that client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="emailLogId">The log entry's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entry, or null when it does not exist or belongs to another client.</returns>
    /// <remarks>
    /// The client is part of the lookup rather than checked afterwards on purpose. An entry holds
    /// the rendered body of a message — invoices, password resets, ticket replies — so a read by
    /// id alone would let anyone with a number read another account's correspondence. Not found
    /// and not yours are deliberately the same answer, which is also what stops the id space
    /// being probed for which entries exist.
    /// </remarks>
    Task<EmailLog?> FindByClientIdAsync(int clientId, int emailLogId, CancellationToken ct);
}
