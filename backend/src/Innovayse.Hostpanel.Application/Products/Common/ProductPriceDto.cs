using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>A product's stored prices in one currency.</summary>
/// <param name="CurrencyCode">ISO 4217 alpha code.</param>
/// <param name="Monthly">Monthly price, or null when the product does not bill monthly in this currency.</param>
/// <param name="Annual">Annual price, or null when the product does not bill annually in this currency.</param>
/// <param name="Quarterly">Quarterly price, or null when the product does not bill quarterly in this currency.</param>
/// <param name="SemiAnnual">Semi-annual price, or null when the product does not bill every six months in this currency.</param>
/// <param name="Biennial">Biennial price, or null when the product does not bill every two years in this currency.</param>
public sealed record ProductPriceDto(
    string CurrencyCode,
    decimal? Monthly,
    decimal? Annual,
    decimal? Quarterly,
    decimal? SemiAnnual,
    decimal? Biennial)
{
    /// <summary>Builds the prices of one currency by asking for each billing cycle in turn.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code.</param>
    /// <param name="amountFor">The amount of a cycle, or null when it is not sold.</param>
    /// <returns>The prices, one optional amount per cycle.</returns>
    public static ProductPriceDto Of(string currencyCode, Func<BillingCycle, decimal?> amountFor)
    {
        ArgumentNullException.ThrowIfNull(amountFor);
        return new(
            currencyCode,
            amountFor(BillingCycle.Monthly),
            amountFor(BillingCycle.Annual),
            amountFor(BillingCycle.Quarterly),
            amountFor(BillingCycle.SemiAnnual),
            amountFor(BillingCycle.Biennial));
    }

    /// <summary>Reads the price of one billing cycle.</summary>
    /// <param name="cycle">The billing cycle.</param>
    /// <returns>The price, or null when the product is not sold for that cycle in this currency.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cycle has no price field.</exception>
    public decimal? For(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Monthly => Monthly,
        BillingCycle.Annual => Annual,
        BillingCycle.Quarterly => Quarterly,
        BillingCycle.SemiAnnual => SemiAnnual,
        BillingCycle.Biennial => Biennial,
        _ => throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "No price field for this cycle."),
    };
}
