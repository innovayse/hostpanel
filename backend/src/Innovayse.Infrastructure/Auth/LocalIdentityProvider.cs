namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Reads people from this product's own <c>AspNetUsers</c> table.
///
/// <para>
/// What a deployment with no SSO gets, and what this product has always done — the same
/// queries behind a name the domain can also satisfy from somewhere else. The subject is
/// the local Identity id, so nothing downstream can tell which provider answered.
/// </para>
/// </summary>
public sealed class LocalIdentityProvider(UserManager<AppUser> users) : IIdentityProvider
{
    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct) =>
        Map(await users.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == subject, ct));

    /// <inheritdoc/>
    public async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        // Identity stores an upper-cased copy for exactly this, so matching on it is both
        // case-insensitive and index-friendly — unlike a ToLower() on the stored column,
        // which cannot use the index.
        var normalized = users.NormalizeEmail(email);
        return Map(await users.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetEmailsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct)
    {
        var ids = subjects.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();

        var rows = await users.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Email != null)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => r.Email!);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<IdentityAccount> Items, int Total)> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = users.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(u =>
                (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)) ||
                u.FirstName.ToUpper().Contains(term) ||
                u.LastName.ToUpper().Contains(term));
        }

        // Counted before paging, so the caller can say "page 2 of 9" rather than only
        // what it happens to be holding.
        var total = await query.CountAsync(ct);

        var items = await query
            // Ordered by something stable: without it, two requests for the same page can
            // return different rows, which reads as data appearing and vanishing.
            .OrderBy(u => u.Id)
            .Skip(Math.Max(page - 1, 0) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.Select(u => Map(u)!).ToList(), total);
    }

    private static IdentityAccount? Map(AppUser? user) =>
        user is null ? null : new IdentityAccount(
            Subject: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName,
            // Identity's own flag is not used here: this product stores the secret itself,
            // so a configured secret is what "enabled" means.
            TwoFactorEnabled: !string.IsNullOrEmpty(user.TwoFactorSecret));
}
