namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>Returns products filtered by group and/or active status.</summary>
/// <param name="GroupId">Optional group filter. When <see langword="null"/>, all groups are returned.</param>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active products.</param>
public record GetProductsQuery(int? GroupId = null, bool ActiveOnly = true);
