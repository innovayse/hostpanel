using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>Extension methods for mapping <see cref="Product"/> to DTOs.</summary>
public static class ProductExtensions
{
    /// <summary>Maps a product to its list DTO, pricing it in the caller's currency.</summary>
    /// <param name="product">The product to project.</param>
    /// <param name="callerCurrencyCode">ISO 4217 code of the currency the caller is billed in.</param>
    /// <returns>The DTO the API answers with.</returns>
    public static ProductDto ToDto(this Product product, string callerCurrencyCode)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(callerCurrencyCode);

        return new ProductDto(
            product.Id,
            product.GroupId,
            product.Name,
            product.Description,
            product.Website,
            product.Slug,
            product.PackageName,
            product.Type,
            product.Status,
            product.ToPricingDto(callerCurrencyCode),
            product.ToPriceDtos(),
            product.ServerGroupId,
            product.IsFeatured,
            product.SortOrder,
            product.Sku,
            product.Metadata,
            product.GracePeriodDays);
    }

    /// <summary>Maps a product to the facts a billed line keeps a copy of.</summary>
    /// <param name="product">The product to project.</param>
    /// <returns>The DTO the billing modules build a line's product snapshot from.</returns>
    public static ProductFulfilmentDto ToFulfilmentDto(this Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductFulfilmentDto(product.Id, product.IsDigital, product.Sku, product.Metadata);
    }

    /// <summary>Maps a product to its storefront catalogue DTO, pricing it in the caller's currency.</summary>
    /// <param name="product">The product to project.</param>
    /// <param name="callerCurrencyCode">ISO 4217 code of the currency the caller is billed in.</param>
    /// <returns>The DTO the storefront reads, with no provisioning field.</returns>
    public static CatalogProductDto ToCatalogDto(this Product product, string callerCurrencyCode)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(callerCurrencyCode);

        return new CatalogProductDto(
            product.Id,
            product.GroupId,
            product.Name,
            product.Description,
            product.Website,
            product.Slug,
            product.Type,
            product.ToPricingDto(callerCurrencyCode),
            product.ToPriceDtos(),
            product.IsFeatured,
            product.SortOrder);
    }

    /// <summary>
    /// Clears the recommended-plan flag on every other product in this product's group, so the
    /// group keeps at most one featured plan. Loads the siblings into the context; the caller's
    /// save persists the change together with the product itself.
    /// </summary>
    /// <param name="product">The product that is now featured.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the siblings are updated in the context.</returns>
    public static async Task UnfeatureSiblingsAsync(
        this Product product, IHostpanelDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(context);

        var siblings = await context.Products
            .Where(p => p.GroupId == product.GroupId && p.Id != product.Id && p.IsFeatured)
            .ToListAsync(ct);
        foreach (var sibling in siblings)
        {
            sibling.Unfeature();
        }
    }

    /// <summary>Prices a product's monthly and annual cycles in one currency.</summary>
    /// <param name="product">The product to price.</param>
    /// <param name="currencyCode">ISO 4217 code of the currency to price in.</param>
    /// <returns>The two cycle prices; a cycle not sold in that currency is null.</returns>
    private static ProductPricingDto ToPricingDto(this Product product, string currencyCode) =>
        new(
            product.PriceFor(currencyCode, BillingCycle.Monthly),
            product.PriceFor(currencyCode, BillingCycle.Annual));

    /// <summary>Groups a product's stored prices by currency, one DTO per currency.</summary>
    /// <param name="product">The product whose prices are read.</param>
    /// <returns>One entry per currency, in the order the currencies first appear.</returns>
    private static IReadOnlyList<ProductPriceDto> ToPriceDtos(this Product product) =>
        product.Prices
            .GroupBy(p => p.CurrencyCode)
            .Select(g => new ProductPriceDto(
                g.Key,
                g.FirstOrDefault(p => p.Cycle == BillingCycle.Monthly)?.Amount,
                g.FirstOrDefault(p => p.Cycle == BillingCycle.Annual)?.Amount))
            .ToList();
}
