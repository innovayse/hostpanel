using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// Reads people from the sign-on provider, which owns them.
///
/// <para>
/// Creates nothing and caches nothing. This product stores no person records in single sign-on mode,
/// so there is no local copy to write and none to go stale, which is the failure the
/// arrangement it replaces had: a shadow row written once at first sign-in and never
/// updated again.
/// </para>
/// </summary>
public sealed class SsoIdentityProvider : IIdentityProvider
{
    /// <summary>The sign-on provider's service API, where every person lookup goes.</summary>
    private readonly SsoServiceClient _sso;

    /// <summary>Initializes a new instance of the <see cref="SsoIdentityProvider"/> class.</summary>
    /// <param name="sso">The sign-on provider's service API, where every person lookup goes.</param>
    public SsoIdentityProvider(SsoServiceClient sso)
    {
        _sso = sso;
    }

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct) =>
        Map(await _sso.GetByIdAsync(subject, ct));

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct) =>
        Map(await _sso.GetByEmailAsync(email, ct));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IdentityAccount>> GetAccountsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct)
    {
        var accounts = await _sso.GetBatchAsync(subjects.Distinct().ToList(), ct);
        return accounts.ToDictionary(kv => kv.Key, kv => Map(kv.Value)!);
    }

    /// <inheritdoc/>
    public async Task<PaginatedList<IdentityAccount>> ListAsync(IdentityAccountListQuery query, CancellationToken ct)
    {
        var page = await _sso.ListAsync(query, ct);
        return new PaginatedList<IdentityAccount>(
            page.Items.Select(a => Map(a)!).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
    }

    // Language is left at its default null on purpose. Where the sign-on provider owns the person it
    // owns their language too, and hostpanel's service view of an external sign-on provider account does not carry
    // one, so there is nothing here to answer with, and answering "English" would be a
    // guess dressed up as a stored preference.

    /// <summary>Projects an external sign-on provider account onto the provider-neutral <see cref="IdentityAccount"/>.</summary>
    /// <param name="account">The account the sign-on provider returned, or null when it knows none.</param>
    /// <returns>The projected account, or null when <paramref name="account"/> is null.</returns>
    private static IdentityAccount? Map(SsoAccount? account) =>
        account is null ? null : new IdentityAccount(
            account.Subject, account.Email, account.FirstName, account.LastName, account.TwoFactorEnabled);
}
