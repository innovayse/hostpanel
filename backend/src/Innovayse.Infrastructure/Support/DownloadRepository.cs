namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IDownloadRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class DownloadRepository(AppDbContext db) : IDownloadRepository
{
    /// <inheritdoc/>
    public async Task<Download?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Downloads.FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc/>
    public void Add(Download download) => db.Downloads.Add(download);

    /// <inheritdoc/>
    public void Remove(Download download) => db.Downloads.Remove(download);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Download>> ListByCategoryAsync(int? categoryId, CancellationToken ct)
    {
        var query = db.Downloads.AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(d => d.CategoryId == categoryId.Value);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<DownloadCategory?> FindCategoryByIdAsync(int id, CancellationToken ct) =>
        await db.DownloadCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public void AddCategory(DownloadCategory category) => db.DownloadCategories.Add(category);

    /// <inheritdoc/>
    public void RemoveCategory(DownloadCategory category) => db.DownloadCategories.Remove(category);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DownloadCategory>> ListCategoriesAsync(CancellationToken ct) =>
        await db.DownloadCategories
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken ct) =>
        await db.Downloads.CountAsync(d => d.CategoryId == categoryId, ct);
}
