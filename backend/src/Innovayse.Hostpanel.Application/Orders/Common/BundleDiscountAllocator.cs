using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>
/// The one place a product bundle's discount is spread over the lines of an order. Pure: it knows no database,
/// no rate table and no client group, so the order flow converts a fixed discount first and hands the figures in.
/// </summary>
/// <remarks>
/// The prices it receives are the prices the payer would otherwise be billed, which already carry the discount of
/// the payer's client group, so the bundle discount comes on top of the group discount and never replaces it.
/// </remarks>
public static class BundleDiscountAllocator
{
    /// <summary>Takes a bundle's discount off the prices of an order's lines.</summary>
    /// <param name="prices">The lines' prices in <paramref name="currency"/>, in line order.</param>
    /// <param name="type">How the bundle is discounted.</param>
    /// <param name="amount">
    /// The discount: a percentage for <see cref="BundleDiscountType.Percentage"/>; for
    /// <see cref="BundleDiscountType.Fixed"/> an amount already converted to <paramref name="currency"/>; ignored for
    /// <see cref="BundleDiscountType.None"/>.
    /// </param>
    /// <param name="currency">ISO 4217 alpha code the prices are in.</param>
    /// <returns>
    /// The prices after the discount, in the same order and never below zero. A percentage comes off every price
    /// (<see cref="PriceDiscount.Apply"/>). A fixed amount is capped at the sum of the prices, split in proportion to
    /// the prices by cumulative rounding to the currency's minor unit, so the shares add up to the amount exactly and no
    /// price goes below zero.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The type is not a defined discount type.</exception>
    public static IReadOnlyList<decimal> Allocate(
        IReadOnlyList<decimal> prices, BundleDiscountType type, decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(prices);

        return type switch
        {
            BundleDiscountType.None => prices,
            BundleDiscountType.Percentage => prices.Select(price => PriceDiscount.Apply(price, amount, currency)).ToList(),
            BundleDiscountType.Fixed => AllocateFixed(prices, amount, currency),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No allocation for this discount type."),
        };
    }

    /// <summary>Splits a fixed amount over the prices in proportion to them.</summary>
    /// <param name="prices">The lines' prices in <paramref name="currency"/>.</param>
    /// <param name="amount">The fixed discount in <paramref name="currency"/>.</param>
    /// <param name="currency">ISO 4217 alpha code the prices are in.</param>
    /// <returns>The prices after the discount.</returns>
    private static IReadOnlyList<decimal> AllocateFixed(IReadOnlyList<decimal> prices, decimal amount, string currency)
    {
        var subtotal = prices.Sum();
        var discount = Math.Min(CurrencyCode.RoundToMinorUnits(amount, currency), subtotal);
        if (discount <= 0m)
        {
            return prices;
        }

        // Each line takes the rounded cumulative share up to itself minus what the lines before it took. The running
        // total therefore never drifts from the exact one by more than half a minor unit, the last line closes on the
        // discount exactly, and a share is never negative because a cumulative share never falls.
        var shares = new decimal[prices.Count];
        var cumulativePrice = 0m;
        var allocated = 0m;
        for (var i = 0; i < prices.Count; i++)
        {
            cumulativePrice += prices[i];
            var cumulativeShare = CurrencyCode.RoundToMinorUnits(discount * cumulativePrice / subtotal, currency);
            shares[i] = cumulativeShare - allocated;
            allocated = cumulativeShare;
        }

        return prices.Select((price, i) => price - shares[i]).ToList();
    }
}
