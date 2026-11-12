namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The one rule for a percentage discount on a price: the bound a discount stays within, the check of it, and the
/// price it leaves. A client group's discount and the discount an invoice line records both use it, so the two cannot
/// drift apart on what a valid discount is or how its amount is rounded.
/// </summary>
public static class PriceDiscount
{
    /// <summary>The largest discount, in percent. The smallest is 0.</summary>
    public const decimal MaxPercent = 100m;

    /// <summary>Whether a discount is within 0 to <see cref="MaxPercent"/> percent.</summary>
    /// <param name="percent">The discount in percent.</param>
    /// <returns>
    /// <see langword="true"/> when the discount is not negative and is at most <see cref="MaxPercent"/>.
    /// </returns>
    public static bool IsValidPercent(decimal percent)
    {
        return percent is >= 0m and <= MaxPercent;
    }

    /// <summary>Takes a discount off a list price, rounded to the currency's minor unit.</summary>
    /// <param name="listPrice">The price before the discount, in <paramref name="currency"/>.</param>
    /// <param name="percent">The discount in percent, from 0 to <see cref="MaxPercent"/>.</param>
    /// <param name="currency">ISO 4217 alpha code the price is in.</param>
    /// <returns>The price after the discount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The discount is outside 0 to <see cref="MaxPercent"/>.</exception>
    public static decimal Apply(decimal listPrice, decimal percent, string currency)
    {
        if (!IsValidPercent(percent))
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "A discount is between 0 and 100 percent.");
        }

        return CurrencyCode.RoundToMinorUnits(listPrice * (1m - (percent / MaxPercent)), currency);
    }
}
