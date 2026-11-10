using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads people from wherever they live: this product's own database, or the external sign-on provider.
///
/// <para>
/// Read-only on purpose. In external sign-on mode this product stores no person records at all, so
/// there is no create, no update and no delete to offer; an interface that had them would
/// need half its surface to throw. Registration, passwords and profile edits belong to
/// whoever owns the accounts.
/// </para>
/// </summary>
public interface IIdentityProvider
{
    /// <summary>The person with this subject, or null if the provider does not know them.</summary>
    /// <param name="subject">The provider's identifier for the person.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, or <see langword="null"/> when the provider does not know the subject.</returns>
    public Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct);

    /// <summary>The person with this email address, or null.</summary>
    /// <param name="email">The address to look up, matched without regard to case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, or <see langword="null"/> when no live account has the address.</returns>
    public Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Full accounts for many subjects at once, keyed by subject. Subjects the provider
    /// does not know are absent.
    ///
    /// <para>
    /// Exists because the admin screens resolve a page of client rows at a time, and doing
    /// that one lookup per row is a round trip per row; over the external sign-on provider's HTTP API, that is
    /// the difference between one request and fifty.
    /// </para>
    /// </summary>
    /// <param name="subjects">The subjects to resolve; duplicates are resolved once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The known accounts, keyed by subject.</returns>
    public Task<IReadOnlyDictionary<string, IdentityAccount>> GetAccountsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct);

    /// <summary>One page of people, optionally filtered, with the unpaged total.</summary>
    /// <param name="query">The search text and the page to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of accounts with the total across all pages.</returns>
    public Task<PaginatedList<IdentityAccount>> ListAsync(IdentityAccountListQuery query, CancellationToken ct);
}
