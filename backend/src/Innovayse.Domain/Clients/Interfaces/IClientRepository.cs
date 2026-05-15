namespace Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Client"/> aggregate operations.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// Finds a client by their primary key.
    /// Returns <see langword="null"/> if not found.
    /// </summary>
    /// <param name="id">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client with contacts loaded, or <see langword="null"/>.</returns>
    Task<Client?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds the client linked to a given Identity user.
    /// Returns <see langword="null"/> if no client record exists for the user.
    /// </summary>
    /// <param name="userId">The Identity user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client with contacts loaded, or <see langword="null"/>.</returns>
    Task<Client?> FindByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of clients with optional filters.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="search">Optional search term (name/company).</param>
    /// <param name="phone">Optional phone partial match.</param>
    /// <param name="status">Optional status filter (parsed enum value).</param>
    /// <param name="userIds">Optional user ID whitelist (for email filtering).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search,
        string? phone, ClientStatus? status,
        IEnumerable<string>? userIds,
        CancellationToken ct);

    /// <summary>
    /// Adds a new client to the repository.
    /// Call <c>IUnitOfWork.SaveChangesAsync</c> to persist.
    /// </summary>
    /// <param name="client">The new client aggregate.</param>
    void Add(Client client);

    /// <summary>
    /// Returns all clients whose account was created within the given date range.
    /// Used for client growth reports.
    /// </summary>
    /// <param name="start">Range start (inclusive, UTC).</param>
    /// <param name="end">Range end (inclusive, UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Clients created within the date range.</returns>
    Task<IReadOnlyList<Client>> GetCreatedBetweenAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);

    /// <summary>
    /// Returns a mapping of user IDs to client IDs for the given user IDs.
    /// </summary>
    /// <param name="userIds">The Identity user IDs to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping user ID to client ID.</returns>
    Task<Dictionary<string, int>> FindClientIdsByUserIdsAsync(IEnumerable<string> userIds, CancellationToken ct);

    /// <summary>
    /// Returns the total count of all client accounts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of clients.</returns>
    Task<int> CountAllAsync(CancellationToken ct);

    /// <summary>
    /// Returns all clients matching the given IDs.
    /// Used for batch lookups to avoid N+1 queries.
    /// </summary>
    /// <param name="ids">Client IDs to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of matched clients.</returns>
    Task<IReadOnlyList<Client>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken ct);
}
