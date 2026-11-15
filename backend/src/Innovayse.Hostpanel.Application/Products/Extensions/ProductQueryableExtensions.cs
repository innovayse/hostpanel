using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The narrowing and the order the staff product reads share: the unpaged picker (<c>GetProductsQuery</c>) and the paged
/// product list (<c>ListProductsQuery</c>) filter by group and status and sort the same way, so each is written once.
/// </summary>
public static class ProductQueryableExtensions
{
    /// <summary>Keeps the products in the group, of the type and in the status; a filter left out lets every product through.</summary>
    /// <param name="products">The products to narrow.</param>
    /// <param name="groupId">The product group the product must belong to, or null for any.</param>
    /// <param name="status">The status the product must be in, or null for any.</param>
    /// <param name="type">The product type the product must be, or null for any.</param>
    /// <returns>The narrowed query.</returns>
    public static IQueryable<Product> Matching(
        this IQueryable<Product> products, int? groupId, ProductStatus? status = null, ProductType? type = null)
    {
        ArgumentNullException.ThrowIfNull(products);

        if (groupId is { } group)
        {
            products = products.Where(p => p.GroupId == group);
        }

        if (status is { } wantedStatus)
        {
            products = products.Where(p => p.Status == wantedStatus);
        }

        if (type is { } wantedType)
        {
            products = products.Where(p => p.Type == wantedType);
        }

        return products;
    }

    /// <summary>Orders the products the way staff see them: the operator's position first, then the name, then the id.</summary>
    /// <param name="products">The products to order.</param>
    /// <returns>The ordered query; the id ends the order so a page never repeats or skips a row between requests.</returns>
    public static IOrderedQueryable<Product> InDisplayOrder(this IQueryable<Product> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        return products.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ThenBy(p => p.Id);
    }
}
