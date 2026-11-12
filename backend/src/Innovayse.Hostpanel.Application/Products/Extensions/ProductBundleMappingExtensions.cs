using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The one place a <see cref="ProductBundle"/> is mapped to and from its DTOs, for staff and for the
/// storefront alike, so both read the same components in the same order.
/// </summary>
public static class ProductBundleMappingExtensions
{
    /// <summary>Builds the bundle items for the components an admin sent.</summary>
    /// <param name="inputs">The components as sent.</param>
    /// <returns>One new item per component.</returns>
    public static IReadOnlyCollection<ProductBundleItem> ToItems(this IReadOnlyList<ProductBundleItemInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        return inputs.Select(i => ProductBundleItem.Create(i.ProductId, i.IsPrimary)).ToList();
    }

    /// <summary>Maps a bundle to the staff DTO.</summary>
    /// <param name="bundle">The bundle, loaded with its items.</param>
    /// <param name="names">The product names by product id, from <see cref="ProductBundleItemQueries.ProductNamesAsync"/>.</param>
    /// <returns>The staff view of the bundle.</returns>
    public static ProductBundleDto ToDto(this ProductBundle bundle, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(names);

        return new ProductBundleDto(
            bundle.Id,
            bundle.Name,
            bundle.Description,
            bundle.IsActive,
            bundle.SortOrder,
            bundle.IncludesDomain,
            bundle.DomainYears,
            bundle.DiscountType,
            bundle.DiscountAmount,
            bundle.DiscountCurrency,
            ToComponents(bundle, names));
    }

    /// <summary>Maps a bundle to the storefront DTO.</summary>
    /// <param name="bundle">The bundle, loaded with its items.</param>
    /// <param name="names">The product names by product id, from <see cref="ProductBundleItemQueries.ProductNamesAsync"/>.</param>
    /// <returns>The storefront view of the bundle, without the fields only staff should see.</returns>
    public static CatalogProductBundleDto ToCatalogDto(this ProductBundle bundle, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(names);

        return new CatalogProductBundleDto(
            bundle.Id,
            bundle.Name,
            bundle.Description,
            bundle.IncludesDomain,
            bundle.DomainYears,
            bundle.DiscountType,
            bundle.DiscountAmount,
            bundle.DiscountCurrency,
            ToComponents(bundle, names));
    }

    /// <summary>Lists the bundle's components, the primary one first, then by name.</summary>
    /// <param name="bundle">The bundle, loaded with its items.</param>
    /// <param name="names">The product names by product id.</param>
    /// <returns>The components.</returns>
    private static List<ProductBundleComponentDto> ToComponents(ProductBundle bundle, IReadOnlyDictionary<int, string> names)
    {
        return bundle.Items
            .Select(i => new ProductBundleComponentDto(i.ProductId, names[i.ProductId], i.IsPrimary))
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }
}
