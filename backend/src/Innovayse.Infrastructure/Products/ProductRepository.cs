namespace Innovayse.Infrastructure.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductRepository"/>.</summary>
public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    /// <summary>
    /// Every read includes the per-currency prices: a product's price is a stored row per currency,
    /// and a product loaded without them answers <c>PriceFor</c> with nothing for every currency.
    /// </summary>
    private IQueryable<Product> Priced => db.Products.Include(p => p.Prices);

    /// <inheritdoc/>
    public async Task<Product?> FindByIdAsync(int id, CancellationToken ct) =>
        await Priced.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Product>> ListAsync(int? groupId, bool activeOnly, CancellationToken ct)
    {
        var query = Priced;

        if (groupId.HasValue)
        {
            query = query.Where(p => p.GroupId == groupId.Value);
        }

        if (activeOnly)
        {
            query = query.Where(p => p.Status == ProductStatus.Active);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Product>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken ct) =>
        await Priced.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Product product) => db.Products.Add(product);
}
