namespace Innovayse.Domain.Products.Interfaces;

using Innovayse.Domain.Products;

/// <summary>
/// Persistence contract for <see cref="ProductGroup"/> aggregate operations.
/// </summary>
public interface IProductGroupRepository
{
    /// <summary>Finds a product group by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="ProductGroup"/>, or <see langword="null"/> if not found.</returns>
    Task<ProductGroup?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all product groups (active and inactive).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of all product groups.</returns>
    Task<IReadOnlyList<ProductGroup>> ListAsync(CancellationToken ct);

    /// <summary>Adds a new product group. Call SaveChangesAsync to persist.</summary>
    /// <param name="group">The new group aggregate.</param>
    void Add(ProductGroup group);
}
