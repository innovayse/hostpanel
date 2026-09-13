namespace Innovayse.Application.Products.Extensions;

using Innovayse.Application.Products.Common;
using Innovayse.Domain.Products;

/// <summary>Writes an admin's price list onto a <see cref="Product"/>.</summary>
public static class ProductPriceInputExtensions
{
    /// <summary>
    /// Makes the product's stored prices match this list exactly.
    /// </summary>
    /// <remarks>
    /// The list is the whole truth: a currency the admin left out is one the product no longer
    /// sells in, and a cycle left null within a currency is removed the same way. Both the
    /// create and the update handler apply the same rule, so it lives once, here.
    /// </remarks>
    /// <param name="prices">The prices the admin entered, one entry per currency.</param>
    /// <param name="product">The product to write them onto.</param>
    public static void ApplyTo(this IReadOnlyList<ProductPriceInput> prices, Product product)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(product);

        var wanted = prices.Select(p => p.CurrencyCode.ToUpperInvariant()).ToHashSet();
        foreach (var existing in product.Prices.Select(p => p.CurrencyCode).Distinct().ToList())
        {
            if (!wanted.Contains(existing))
            {
                product.RemovePrices(existing);
            }
        }

        foreach (var input in prices)
        {
            product.RemovePrices(input.CurrencyCode);
            if (input.Monthly is { } monthly)
            {
                product.SetPrice(input.CurrencyCode, BillingCycle.Monthly, monthly);
            }

            if (input.Annual is { } annual)
            {
                product.SetPrice(input.CurrencyCode, BillingCycle.Annual, annual);
            }
        }
    }
}
