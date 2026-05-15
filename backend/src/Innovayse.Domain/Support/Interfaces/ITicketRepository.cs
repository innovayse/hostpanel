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
}
