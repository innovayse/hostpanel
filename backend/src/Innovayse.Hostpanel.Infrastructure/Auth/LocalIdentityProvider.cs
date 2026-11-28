using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// Reads people from this product's own <c>AspNetUsers</c> table.
///
/// <para>
/// What a deployment with no external sign-on provider gets, and what this product has always done: the same
/// queries behind a name the domain can also satisfy from somewhere else. The subject is
/// the local Identity id, so nothing downstream can tell which provider answered.
/// </para>
/// </summary>
public sealed class LocalIdentityProvider : IIdentityProvider
{
    /// <summary>Identity's user store, read for the people this deployment owns.</summary>
    private readonly UserManager<AppUser> _users;

    /// <summary>Initializes a new instance of the <see cref="LocalIdentityProvider"/> class.</summary>
    /// <param name="users">Identity's user store, read for the people this deployment owns.</param>
    public LocalIdentityProvider(UserManager<AppUser> users)
    {
        _users = users;
    }

    /// <summary>
    /// Live accounts only. A deleted one keeps its row so the records that reference it
    /// still say who they belonged to, but it is not a person this product will hand back:
    /// every caller here is asking about somebody who still has an account.
    /// </summary>
    private IQueryable<AppUser> Live => _users.Users.AsNoTracking().Where(u => u.DeletedAt == null);

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct) =>
        Map(await Live.FirstOrDefaultAsync(u => u.Id == subject, ct));

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        // Identity stores an upper-cased copy for exactly this, so matching on it is both
        // case-insensitive and index-friendly, unlike a ToLower() on the stored column,
        // which cannot use the index.
        var normalized = _users.NormalizeEmail(email);
        return Map(await Live.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IdentityAccount>> GetAccountsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct)
    {
        var ids = subjects.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, IdentityAccount>();
        }

        var rows = await Live.Where(u => ids.Contains(u.Id)).ToListAsync(ct);

        return rows.ToDictionary(u => u.Id, u => Map(u)!);
    }

    /// <inheritdoc/>
    public async Task<PaginatedList<IdentityAccount>> ListAsync(IdentityAccountListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var accounts = Live;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            accounts = accounts.Where(u =>
                (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)) ||
                u.FirstName.ToUpper().Contains(term) ||
                u.LastName.ToUpper().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToUpper().Contains(term));
        }

        // Counted before paging, so the caller can say "page 2 of 9" rather than only
        // what it happens to be holding.
        var total = await accounts.CountAsync(ct);

        var items = await accounts
            // Ordered by something stable: without it, two requests for the same page can
            // return different rows, which reads as data appearing and vanishing.
            .OrderBy(u => u.Id)
            .Skip(PageOffset.For(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedList<IdentityAccount>(
            items.Select(u => Map(u)!).ToList(), total, query.Page, query.PageSize);
    }

    /// <summary>Projects a local account row onto the provider-neutral <see cref="IdentityAccount"/>.</summary>
    /// <param name="user">The local account, or null when none was found.</param>
    /// <returns>The projected account, or null when <paramref name="user"/> is null.</returns>
    private static IdentityAccount? Map(AppUser? user) =>
        user is null ? null : new IdentityAccount(
            Subject: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName,
            // Identity's own flag is not used here: this product stores the secret itself,
            // so a configured secret is what "enabled" means.
            TwoFactorEnabled: !string.IsNullOrEmpty(user.TwoFactorSecret),
            LastLoginAt: user.LastLoginAt,
            // This deployment owns the account row, so the language on it is ours to answer
            // with. Without this the portal read a hard-coded default and every saved
            // language came back as English on the next load.
            Language: user.Language,
            // Read from the row, not the token: the claim is baked in at sign-in and goes stale
            // when the address is confirmed afterwards (for example by claiming setup).
            EmailVerified: user.EmailConfirmed);
}
