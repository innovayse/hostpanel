namespace Innovayse.Application.Products.Queries.GetProductFeatures;

using Innovayse.Domain.Products.Interfaces;

/// <summary>
/// Handles <see cref="GetProductFeaturesQuery"/>.
/// </summary>
/// <remarks>
/// The products are resolved first and their lines read in one pass, so the
/// storefront can build a comparison table from a single request rather than one
/// per plan.
/// </remarks>
/// <param name="products">Product repository.</param>
/// <param name="features">Product feature repository.</param>
public sealed class GetProductFeaturesHandler(
    IProductRepository products,
    IProductFeatureRepository features)
{
    /// <summary>
    /// Returns the specification lines of the matching products.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list ordered by product, then by sort order.</returns>
    public async Task<IReadOnlyList<ProductFeatureDto>> HandleAsync(
        GetProductFeaturesQuery query,
        CancellationToken ct)
    {
        var matching = await products.ListAsync(query.GroupId, query.ActiveOnly, ct);

        var ids = query.ProductId.HasValue
            ? matching.Where(p => p.Id == query.ProductId.Value).Select(p => p.Id)
            : matching.Select(p => p.Id);

        var lines = await features.ListForProductsAsync(ids, ct);

        return lines
            .Select(f => new ProductFeatureDto(f.Id, f.ProductId, f.Label, f.Value, f.SortOrder))
            .ToList();
    }
}
