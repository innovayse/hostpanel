namespace Innovayse.Application.Products.Extensions;

using Innovayse.Application.Products.Queries.GetProducts;
using Innovayse.Domain.Products;

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
            new ProductPricingDto(
                product.PriceFor(callerCurrencyCode, BillingCycle.Monthly),
                product.PriceFor(callerCurrencyCode, BillingCycle.Annual)),
            product.ToPriceDtos(),
            product.ServerGroupId);
    }

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
