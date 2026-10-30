using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// Reads about product SKUs. Shared by the create and update handlers so the uniqueness question,
/// asked before the save and again after a refused save, is phrased once.
/// </summary>
/// <remarks>
/// A SKU names one product only: a fulfilment plugin keys its delivery on it, so two products under
/// one SKU would make a purchase of either deliver the same thing. The rule spans aggregates, so the
/// handler asks it; the filtered unique index <c>IX_Products_Sku</c> is what holds it under a race.
/// </remarks>
public static class ProductSkuQueries
{
    /// <summary>Whether a product other than <paramref name="excludingId"/> already holds the SKU.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="sku">The SKU in any case; it is normalised the way <see cref="Product.SetSku"/> stores it.</param>
    /// <param name="excludingId">The product to ignore, so an update keeping its own SKU does not clash with itself.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when another product already uses the SKU.</returns>
    public static async Task<bool> ProductSkuInUseAsync(
        this IHostpanelDbContext context, string sku, int? excludingId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sku);

        var normalized = ProductSku.Normalize(sku);
        if (normalized is null)
        {
            return false;
        }

        return await context.Products.AnyAsync(
            p => p.Sku == normalized && (excludingId == null || p.Id != excludingId),
            ct);
    }
}
