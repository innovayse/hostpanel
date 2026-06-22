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

    /// <summary>
    /// Returns all active services whose next renewal date is on or before <paramref name="asOf"/>.
    /// Used by the renewal invoice cron to generate renewal invoices.
    /// </summary>
    /// <param name="asOf">The reference point in time; services with NextRenewalAt &lt;= this are due.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Active services due for renewal.</returns>
    Task<IReadOnlyList<ClientService>> ListDueForRenewalAsync(DateTimeOffset asOf, CancellationToken ct);

    /// <summary>Returns all client services without pagination. Used for report calculations.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All client service instances in the system.</returns>
    Task<IReadOnlyList<ClientService>> GetAllAsync(CancellationToken ct);

    /// <summary>Finds a client service by client ID and domain name.</summary>
    Task<ClientService?> FindByClientAndDomainAsync(int clientId, string domain, CancellationToken ct);

    /// <summary>
    /// Returns all suspended services whose <see cref="ClientService.SuspendedAt"/>
    /// is on or before <paramref name="threshold"/>.
    /// Used by the auto-terminate cron to terminate long-suspended services.
    /// </summary>
    /// <param name="threshold">Services suspended on or before this point are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Suspended services that have exceeded the suspension limit.</returns>
    Task<List<ClientService>> ListSuspendedBeforeAsync(DateTimeOffset threshold, CancellationToken ct);
}
