namespace Innovayse.Application.Products.Queries.GetProductFeatures;

/// <summary>
/// Requests the specification lines of the products in a group, or of one product.
/// </summary>
/// <param name="GroupId">Optional product group filter.</param>
/// <param name="ProductId">Optional single-product filter.</param>
/// <param name="ActiveOnly">When <see langword="true"/>, considers only active products.</param>
public record GetProductFeaturesQuery(int? GroupId, int? ProductId, bool ActiveOnly = true);
