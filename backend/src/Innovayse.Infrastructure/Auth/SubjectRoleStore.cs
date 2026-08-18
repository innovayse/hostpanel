namespace Innovayse.Infrastructure.Auth;

using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The roles a subject holds, kept in this product's own database in both modes.
/// </summary>
public sealed class SubjectRoleStore(AppDbContext db) : ISubjectRoleStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetRolesAsync(string subject, CancellationToken ct) =>
        await db.SubjectRoles
            .AsNoTracking()
            .Where(x => x.Subject == subject)
            .Select(x => x.Role)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(string subject, string role, CancellationToken ct)
    {
        // Checked rather than caught: a duplicate grant is an ordinary thing for a caller
        // to do, and letting it reach the database would surface as an exception the
        // caller then has to distinguish from a real failure.
        var held = await db.SubjectRoles
            .AnyAsync(x => x.Subject == subject && x.Role == role, ct);
        if (held) return;

        db.SubjectRoles.Add(new SubjectRole { Subject = subject, Role = role });
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string subject, string role, CancellationToken ct)
    {
        await db.SubjectRoles
            .Where(x => x.Subject == subject && x.Role == role)
            .ExecuteDeleteAsync(ct);
    }
}
