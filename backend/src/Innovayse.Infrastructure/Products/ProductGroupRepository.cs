namespace Innovayse.Infrastructure.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductGroupRepository"/>.</summary>
public sealed class ProductGroupRepository(AppDbContext db) : IProductGroupRepository
{
    /// <inheritdoc/>
    public async Task<ProductGroup?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ProductGroups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductGroup>> ListAsync(CancellationToken ct) =>
        await db.ProductGroups
            .Include(g => g.Products)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(ProductGroup group) => db.ProductGroups.Add(group);
}
