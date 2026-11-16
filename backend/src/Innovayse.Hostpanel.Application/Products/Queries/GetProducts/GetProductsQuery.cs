using Innovayse.Hostpanel.Application.Products.Common;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProducts;

/// <summary>
/// Returns the staff view of the products, filtered by group and/or active status, as one unpaged list: the picker the
/// screens that choose a product read. The admin Products screen reads the paged <c>ListProductsQuery</c> instead.
/// </summary>
/// <param name="GroupId">Optional group filter. When <see langword="null"/>, all groups are returned.</param>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active products.</param>
/// <param name="IncludeUnsellable">
/// When <see langword="true"/>, products with no price in the caller's currency are returned too,
/// with <see cref="ProductPricingDto"/> values of <see langword="null"/>; a picker that must
/// offer every product asks for them.
/// </param>
public sealed record GetProductsQuery(
    int? GroupId = null,
    bool ActiveOnly = true,
    bool IncludeUnsellable = false);
