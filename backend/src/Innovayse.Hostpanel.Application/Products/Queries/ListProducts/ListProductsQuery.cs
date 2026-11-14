using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProducts;

/// <summary>
/// Returns one page of the staff view of the products, narrowed by a search text, a group, a type and a status. Every
/// product is listed, the ones with no price in the caller's currency included: the admin grid must show every product.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only products whose name, SKU or slug contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="GroupId">Optional product group filter. Null returns every group.</param>
/// <param name="Type">Optional product type filter. Null returns every type.</param>
/// <param name="Status">Optional status filter. Null returns every status.</param>
public sealed record ListProductsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? GroupId = null,
    ProductType? Type = null,
    ProductStatus? Status = null) : IListQuery;
