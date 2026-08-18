namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Reads people from the SSO, which owns them.
///
/// <para>
/// Creates nothing and caches nothing. This product stores no person records in SSO mode,
/// so there is no local copy to write and none to go stale — which is the failure the
/// arrangement it replaces had: a shadow row written once at first sign-in and never
/// updated again.
/// </para>
/// </summary>
public sealed class SsoIdentityProvider(SsoServiceClient sso) : IIdentityProvider
{
    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct) =>
        Map(await sso.GetByIdAsync(subject, ct));

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct) =>
        Map(await sso.GetByEmailAsync(email, ct));

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> GetEmailsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct) =>
        sso.GetEmailsAsync(subjects.Distinct().ToList(), ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<IdentityAccount> Items, int Total)> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct)
    {
        var (items, total) = await sso.ListAsync(search, page, pageSize, ct);
        return (items.Select(a => Map(a)!).ToList(), total);
    }

    private static IdentityAccount? Map(SsoServiceClient.SsoAccount? account) =>
        account is null ? null : new IdentityAccount(
            account.Subject, account.Email, account.FirstName, account.LastName, account.TwoFactorEnabled);
}
