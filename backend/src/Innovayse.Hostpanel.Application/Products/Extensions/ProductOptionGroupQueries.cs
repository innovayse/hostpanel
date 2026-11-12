using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// Reads about a product's option groups. Shared by the admin and the orderable queries and by the
/// option handlers, so the ordering and the one mapping to <see cref="ProductOptionGroupDto"/> live once.
/// </summary>
public static class ProductOptionGroupQueries
{
    /// <summary>Reads a product's option groups with their options, both in display order.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="productId">Product to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per group, empty groups included; empty when the product has none.</returns>
    public static async Task<IReadOnlyList<ProductOptionGroupDto>> ReadProductOptionGroupsAsync(
        this IHostpanelDbContext context, int productId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await context.ProductOptionGroups
            .AsNoTracking()
            .Where(g => g.ProductId == productId)
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Id)
            .Select(g => new ProductOptionGroupDto(
                g.Id,
                g.ProductId,
                g.Name,
                g.Type,
                g.IsRequired,
                g.DisplayOrder,
                g.MinQuantity,
                g.MaxQuantity,
                g.Options
                    .OrderBy(o => o.DisplayOrder)
                    .ThenBy(o => o.Id)
                    .Select(o => new ProductOptionDto(
                        o.Id, o.Name, o.PriceModifier, o.PriceType, o.SetupFeeModifier, o.DisplayOrder))
                    .ToList()))
            .ToListAsync(ct);
    }

    /// <summary>Loads, for change, the group that holds an option, with all of its options.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="optionId">The option whose group is wanted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked group, or <see langword="null"/> when no group holds the option.</returns>
    public static async Task<ProductOptionGroup?> FindGroupOfOptionAsync(
        this IHostpanelDbContext context, int optionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await context.ProductOptionGroups
            .Include(g => g.Options)
            .FirstOrDefaultAsync(g => g.Options.Any(o => o.Id == optionId), ct);
    }
}
