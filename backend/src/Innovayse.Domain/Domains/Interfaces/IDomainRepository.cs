namespace Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Domain"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IDomainRepository
{
    /// <summary>
    /// Finds a domain by primary key, including nameservers and DNS records.
    /// </summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain with related data loaded, or <see langword="null"/> if not found.</returns>
    Task<Domain?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds a domain by its fully-qualified name.
    /// </summary>
    /// <param name="name">The fully-qualified domain name (e.g. "example.com").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching domain, or <see langword="null"/> if not found.</returns>
    Task<Domain?> FindByNameAsync(string name, CancellationToken ct);

    /// <summary>
    /// Returns all domains owned by a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All domains for the client, ordered by name.</returns>
    Task<IReadOnlyList<Domain>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Returns all active domains that expire before the given threshold date.
    /// Used by scheduled jobs to send expiry notifications.
    /// </summary>
    /// <param name="threshold">Upper-bound expiry date (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Domains expiring before the threshold.</returns>
    Task<IReadOnlyList<Domain>> ListExpiringBeforeAsync(DateTimeOffset threshold, CancellationToken ct);

    /// <summary>
    /// Returns all active domains with auto-renew enabled whose expiry date is before the given threshold.
    /// Used by the auto-renew scheduled job.
    /// </summary>
    /// <param name="threshold">Upper-bound expiry date for auto-renew candidates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Domains due for automatic renewal.</returns>
    Task<IReadOnlyList<Domain>> ListAutoRenewDueAsync(DateTimeOffset threshold, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of domains, optionally filtered by client.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="clientId">Optional client ID to filter by; null returns all domains.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Domain> Items, int TotalCount)> PagedListAsync(int page, int pageSize, CancellationToken ct, int? clientId = null);

    /// <summary>
    /// Returns a dictionary mapping service IDs to their linked domain names.
    /// Only includes domains that have a non-null <see cref="Domain.LinkedServiceId"/>
    /// matching one of the provided <paramref name="serviceIds"/>.
    /// </summary>
    /// <param name="serviceIds">Service IDs to look up linked domains for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping service ID to domain name.</returns>
    Task<Dictionary<int, string>> FindDomainNamesByServiceIdsAsync(IEnumerable<int> serviceIds, CancellationToken ct);

    /// <summary>
    /// Adds a new domain to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="domain">The new domain aggregate.</param>
    void Add(Domain domain);
}
