using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>Every price a product has in one currency, one optional amount per billing cycle.</summary>
/// <param name="CurrencyCode">ISO 4217 alpha code, any case.</param>
/// <param name="Monthly">Monthly amount, or <see langword="null"/> when the product is not sold monthly in this currency.</param>
/// <param name="Annual">Annual amount, or <see langword="null"/> when the product is not sold yearly in this currency.</param>
/// <param name="Quarterly">Quarterly amount, or <see langword="null"/> when the product is not sold quarterly in this currency.</param>
/// <param name="SemiAnnual">Semi-annual amount, or <see langword="null"/> when the product is not sold every six months in this currency.</param>
/// <param name="Biennial">Biennial amount, or <see langword="null"/> when the product is not sold every two years in this currency.</param>
public sealed record ProductCurrencyPrices(
    string CurrencyCode,
    decimal? Monthly,
    decimal? Annual,
    decimal? Quarterly,
    decimal? SemiAnnual,
    decimal? Biennial)
{
    /// <summary>Builds the prices of one currency by asking for each billing cycle in turn.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <param name="amountFor">The amount of a cycle, or <see langword="null"/> when it is not sold.</param>
    /// <returns>The prices, one optional amount per cycle.</returns>
    public static ProductCurrencyPrices Of(string currencyCode, Func<BillingCycle, decimal?> amountFor)
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

    /// <summary>Lists the cycles this currency is priced for.</summary>
    /// <returns>One entry per cycle that has an amount; a cycle left <see langword="null"/> is not listed.</returns>
    public IEnumerable<(BillingCycle Cycle, decimal Amount)> Priced()
    {
        if (Monthly is { } monthly)
        {
            yield return (BillingCycle.Monthly, monthly);
        }

        if (Annual is { } annual)
        {
            yield return (BillingCycle.Annual, annual);
        }

        if (Quarterly is { } quarterly)
        {
            yield return (BillingCycle.Quarterly, quarterly);
        }

        if (SemiAnnual is { } semiAnnual)
        {
            yield return (BillingCycle.SemiAnnual, semiAnnual);
        }

        if (Biennial is { } biennial)
        {
            yield return (BillingCycle.Biennial, biennial);
        }
    }
}
