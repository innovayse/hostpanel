namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="Ticket"/> aggregate persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Finds a ticket by its primary key.
    /// </summary>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Ticket"/>, or <see langword="null"/> if not found.</returns>
    Task<Ticket?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new ticket for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="ticket">The ticket to add.</param>
    void Add(Ticket ticket);

    /// <summary>
    /// Returns a paged list of all tickets ordered by creation date descending.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of tickets for the requested page.</returns>
    Task<IReadOnlyList<Ticket>> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns a paged, filtered list of tickets with total count.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="search">Optional subject search term.</param>
    /// <param name="flaggedOnly">When true, returns only flagged tickets.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of matching tickets and total count.</returns>
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> ListAsync(
        int page, int pageSize, TicketStatus? status, string? search, bool flaggedOnly, CancellationToken ct);

    /// <summary>
    /// Returns all tickets created within a date range, including replies.
    /// </summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of tickets within the range.</returns>
    Task<IReadOnlyList<Ticket>> ListByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    /// <summary>
    /// Returns all tickets belonging to a specific client.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of the client's tickets.</returns>
    Task<IReadOnlyList<Ticket>> ListByClientIdAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Returns the count of tickets in the given status.
    /// </summary>
    /// <param name="status">The status to count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of tickets with the specified status.</returns>
    Task<int> CountByStatusAsync(TicketStatus status, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of tickets for a specific client, optionally filtered by subject.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="search">Optional subject search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of matching tickets and total count.</returns>
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize, string? search, CancellationToken ct);

    /// <summary>
    /// Stages a ticket for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="ticket">The ticket to remove.</param>
    void Remove(Ticket ticket);

    /// <summary>
    /// Returns the total count of all tickets in the system.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of tickets.</returns>
    Task<int> CountAsync(CancellationToken ct);

    /// <summary>
    /// Returns the count of tickets for a client within a date range.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of matching tickets.</returns>
    Task<int> CountByClientIdAndDateRangeAsync(int clientId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
