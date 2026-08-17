namespace Innovayse.Infrastructure.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductFeatureRepository"/>.</summary>
public sealed class ProductFeatureRepository(AppDbContext db) : IProductFeatureRepository
{
    /// <inheritdoc/>
    public async Task<ProductFeature?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ProductFeatures.FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductFeature>> ListForProductsAsync(
        IEnumerable<int> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.ProductFeatures
            .Where(f => ids.Contains(f.ProductId))
            .OrderBy(f => f.ProductId)
            .ThenBy(f => f.SortOrder)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsWithLabelAsync(
        int productId, string label, int? excludingId, CancellationToken ct)
    {
        var needle = label.Trim().ToLower();

        return await db.ProductFeatures.AnyAsync(
            f => f.ProductId == productId
                && f.Label.ToLower() == needle
                && (excludingId == null || f.Id != excludingId),
            ct);
    }

    /// <inheritdoc/>
    public void Add(ProductFeature feature) => db.ProductFeatures.Add(feature);

    /// <inheritdoc/>
    public void Remove(ProductFeature feature) => db.ProductFeatures.Remove(feature);
}
