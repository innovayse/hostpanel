using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// The roles a subject holds, kept in this product's own database in both modes.
/// </summary>
public sealed class SubjectRoleStore : ISubjectRoleStore
{
    /// <summary>The database holding the <c>SubjectRoles</c> rows.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="SubjectRoleStore"/> class.</summary>
    /// <param name="db">The database holding the <c>SubjectRoles</c> rows.</param>
    public SubjectRoleStore(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Role>> GetRolesAsync(string subject, CancellationToken ct) =>
        await _db.SubjectRoles
            .AsNoTracking()
            .Where(x => x.Subject == subject)
            .Select(x => x.Role)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(string subject, Role role, CancellationToken ct)
    {
        // One statement that ignores a row already there, rather than a check followed by an insert:
        // two requests granting the same role at the same moment (a customer's first two orders) both
        // pass a check, and the second insert then broke the key. The key is the pair, so the database
        // is what decides, and a duplicate grant is simply not written.
        var roleName = role.ToString();
        await _db.Database.ExecuteSqlAsync(
            $"""INSERT INTO "SubjectRoles" ("Subject", "Role") VALUES ({subject}, {roleName}) ON CONFLICT ("Subject", "Role") DO NOTHING""",
            ct);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string subject, Role role, CancellationToken ct)
    {
        await _db.SubjectRoles
            .Where(x => x.Subject == subject && x.Role == role)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> AnyHasRoleAsync(Role role, CancellationToken ct) =>
        await _db.SubjectRoles.AnyAsync(x => x.Role == role, ct);

    /// <inheritdoc/>
    public async Task<bool> HoldsAsync(string subject, Role role, CancellationToken ct)
    {
        var granted = await _db.SubjectRoles
            .AsNoTracking()
            .AnyAsync(x => x.Subject == subject && x.Role == role, ct);
        if (granted)
        {
            return true;
        }

        // The Identity role table is the other half of the union GetHoldersAsync answers with.
        var roleName = role.ToString();
        return await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == subject)
            .Join(_db.Roles.Where(r => r.Name == roleName), ur => ur.RoleId, r => r.Id, (ur, _) => ur.UserId)
            .AnyAsync(ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The union of the grants in this store and the Identity role table: a local deployment
    /// authorizes on both (the token the local sign-in mints carries the Identity roles too), so
    /// an administrator granted only there still counts. Under an external sign-on provider the
    /// Identity tables are empty and only this store answers.
    /// </remarks>
    public async Task<IReadOnlyList<string>> GetHoldersAsync(Role role, CancellationToken ct)
    {
        var roleName = role.ToString();
        var granted = await _db.SubjectRoles
            .AsNoTracking()
            .Where(x => x.Role == role)
            .Select(x => x.Subject)
            .ToListAsync(ct);
        var identity = await _db.UserRoles
            .AsNoTracking()
            .Join(_db.Roles.Where(r => r.Name == roleName), ur => ur.RoleId, r => r.Id, (ur, _) => ur.UserId)
            .ToListAsync(ct);

        return granted.Concat(identity).Distinct(StringComparer.Ordinal).ToList();
    }
}
