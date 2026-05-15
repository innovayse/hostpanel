namespace Innovayse.Domain.Services.Interfaces;

using Innovayse.Domain.Services;

/// <summary>
/// Persistence contract for <see cref="ClientService"/> aggregate operations.
/// </summary>
public interface IClientServiceRepository
{
    /// <summary>Finds a client service by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="ClientService"/>, or <see langword="null"/> if not found.</returns>
    Task<ClientService?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all services for a given client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of services belonging to the client.</returns>
    Task<IReadOnlyList<ClientService>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>Returns a paginated list of all client services (admin view).</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated items and total count.</returns>
    Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a paginated list of client services optionally filtered by client (admin view).</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="clientId">Optional client ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated items and total count.</returns>
    Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(int page, int pageSize, int? clientId, CancellationToken ct);

    /// <summary>Adds a new client service. Call SaveChangesAsync to persist.</summary>
    /// <param name="service">The new client service aggregate.</param>
    void Add(ClientService service);

    /// <summary>Returns all client services without pagination. Used for report calculations.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All client service instances in the system.</returns>
    Task<IReadOnlyList<ClientService>> GetAllAsync(CancellationToken ct);
}
