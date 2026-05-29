namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IKbCategoryRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class KbCategoryRepository(AppDbContext db) : IKbCategoryRepository
{
    /// <inheritdoc/>
    public async Task<KbCategory?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.KbCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public void Add(KbCategory category) => db.KbCategories.Add(category);

    /// <inheritdoc/>
    public void Remove(KbCategory category) => db.KbCategories.Remove(category);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KbCategory>> ListAllAsync(CancellationToken ct) =>
        await db.KbCategories
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
}
