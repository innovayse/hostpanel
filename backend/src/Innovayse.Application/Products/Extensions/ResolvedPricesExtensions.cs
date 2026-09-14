namespace Innovayse.Application.Products.Extensions;

using Innovayse.Application.Products.Common;
using Innovayse.Domain.Products;

/// <summary>Writes an admin's resolved price list onto a <see cref="Product"/>.</summary>
public static class ResolvedPricesExtensions
{
    /// <summary>
    /// Makes the product's stored prices match this list.
    /// </summary>
    /// <remarks>
    /// Every currency in the list is rewritten in full: a cycle left null within it is removed,
    /// not kept at its old amount. A currency absent from the list is removed only when
    /// <see cref="ResolvedPrices.ReplaceAllCurrencies"/> is set — the legacy admin form speaks only
    /// for the base currency and must leave the others alone. Both the create and the update
    /// handler apply the same rule, so it lives once, here.
    /// </remarks>
    /// <param name="resolved">The prices the admin entered and how far they reach.</param>
    /// <param name="product">The product to write them onto.</param>
    public static void ApplyTo(this ResolvedPrices resolved, Product product)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(product);

        if (resolved.ReplaceAllCurrencies)
        {
            var wanted = resolved.Prices.Select(p => p.CurrencyCode.ToUpperInvariant()).ToHashSet();
            foreach (var existing in product.Prices.Select(p => p.CurrencyCode).Distinct().ToList())
            {
                if (!wanted.Contains(existing))
                {
                    product.RemovePrices(existing);
                }
            }
        }

        foreach (var input in resolved.Prices)
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
