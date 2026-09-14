namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>Returns products filtered by group and/or active status.</summary>
/// <param name="GroupId">Optional group filter. When <see langword="null"/>, all groups are returned.</param>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active products.</param>
/// <param name="IncludeUnsellable">
/// When <see langword="true"/>, products with no price in the caller's currency are returned too,
/// with <see cref="ProductPricingDto"/> values of <see langword="null"/> — the admin grid must
/// show every product. The storefront (the default) omits them, because a visitor cannot buy them.
/// </param>
public record GetProductsQuery(int? GroupId = null, bool ActiveOnly = true, bool IncludeUnsellable = false);
