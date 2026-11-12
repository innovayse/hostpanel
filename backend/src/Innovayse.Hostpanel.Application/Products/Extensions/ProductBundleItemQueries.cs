using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// Reads about the products a bundle is made of. Shared by the create, update and read handlers so the
/// rules that span aggregates (the product exists, it is not a domain product) are asked once.
/// </summary>
/// <remarks>
/// Rules inside the bundle (at least one item, no product twice, exactly one primary) are the
/// aggregate's own and are not repeated here.
/// </remarks>
public static class ProductBundleItemQueries
{
    /// <summary>Checks the products of the components an admin sent.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="inputs">The components as sent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c> when a product does not exist,
    /// <c>PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED</c> when one is a domain product, otherwise
    /// <see langword="null"/>.
    /// </returns>
    public static async Task<string?> FindBundleItemRefusalAsync(
        this IHostpanelDbContext context, IReadOnlyList<ProductBundleItemInput> inputs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inputs);

        // Distinct so a product listed twice is answered by the aggregate's duplicate refusal, not by a
        // misleading "not found" from a shorter result set.
        var ids = inputs.Select(i => i.ProductId).Distinct().ToList();
        var types = await context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Type)
            .ToListAsync(ct);

        if (types.Count != ids.Count)
        {
            return HostpanelErrorCodes.PRODUCT_BUNDLE_PRODUCT_NOT_FOUND;
        }

        return types.Contains(ProductType.Domain) ? HostpanelErrorCodes.PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED : null;
    }

    /// <summary>Reads the display names of the products the given bundles are made of.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bundles">The bundles to be mapped, loaded with their items.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The product names by product id.</returns>
    public static async Task<IReadOnlyDictionary<int, string>> ProductNamesAsync(
        this IHostpanelDbContext context, IReadOnlyCollection<ProductBundle> bundles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bundles);

        var ids = bundles.SelectMany(b => b.Items).Select(i => i.ProductId).Distinct().ToList();
        return await context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
    }
}
