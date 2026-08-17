namespace Innovayse.Domain.Products.Interfaces;

using Innovayse.Domain.Products;

/// <summary>
/// Persistence contract for <see cref="ProductFeature"/> operations.
/// </summary>
public interface IProductFeatureRepository
{
    /// <summary>Finds a feature line by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="ProductFeature"/>, or <see langword="null"/> if not found.</returns>
    Task<ProductFeature?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns the feature lines of the given products, ordered for display.
    /// </summary>
    /// <param name="productIds">Products whose lines to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list ordered by product, then by sort order.</returns>
    Task<IReadOnlyList<ProductFeature>> ListForProductsAsync(
        IEnumerable<int> productIds, CancellationToken ct);

    /// <summary>
    /// Whether the product already describes a feature under this label.
    /// </summary>
    /// <remarks>
    /// Compared without regard to case, because the storefront groups its comparison
    /// rows by the label as written: "Disk" and "disk" would become two rows that look
    /// like a mistake rather than a distinction.
    /// </remarks>
    /// <param name="productId">Product to look in.</param>
    /// <param name="label">Label to look for.</param>
    /// <param name="excludingId">Line to ignore, so renaming a line does not clash with itself.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when another line already uses the label.</returns>
    Task<bool> ExistsWithLabelAsync(int productId, string label, int? excludingId, CancellationToken ct);

    /// <summary>Adds a new feature line. Call SaveChangesAsync to persist.</summary>
    /// <param name="feature">The new line.</param>
    void Add(ProductFeature feature);

    /// <summary>Removes a feature line. Call SaveChangesAsync to persist.</summary>
    /// <param name="feature">The line to remove.</param>
    void Remove(ProductFeature feature);
}
