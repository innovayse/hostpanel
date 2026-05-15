namespace Innovayse.Domain.Products.Interfaces;

using Innovayse.Domain.Products;

/// <summary>
/// Persistence contract for <see cref="Product"/> aggregate operations.
/// </summary>
public interface IProductRepository
{
    /// <summary>Finds a product by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Product"/>, or <see langword="null"/> if not found.</returns>
    Task<Product?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a list of products, optionally filtered by group or active status.
    /// </summary>
    /// <param name="groupId">Optional group filter.</param>
    /// <param name="activeOnly">When <see langword="true"/>, returns only active products.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only filtered list of products.</returns>
    Task<IReadOnlyList<Product>> ListAsync(int? groupId, bool activeOnly, CancellationToken ct);

    /// <summary>Returns all products matching the given IDs.</summary>
    /// <param name="ids">Product IDs to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of matched products.</returns>
    Task<IReadOnlyList<Product>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken ct);

    /// <summary>Adds a new product. Call SaveChangesAsync to persist.</summary>
    /// <param name="product">The new product aggregate.</param>
    void Add(Product product);
}
