namespace Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="EmailDomain"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IEmailDomainRepository
{
    /// <summary>
    /// Finds an email domain by primary key, including mailboxes and aliases.
    /// </summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The email domain with related data loaded, or <see langword="null"/> if not found.</returns>
    Task<EmailDomain?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds an email domain by its fully-qualified domain name.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name (e.g. "example.com").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching email domain, or <see langword="null"/> if not found.</returns>
    Task<EmailDomain?> FindByDomainNameAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Returns all email domains owned by a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All email domains for the client.</returns>
    Task<IReadOnlyList<EmailDomain>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Adds a new email domain to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="emailDomain">The new email domain aggregate.</param>
    void Add(EmailDomain emailDomain);
}
