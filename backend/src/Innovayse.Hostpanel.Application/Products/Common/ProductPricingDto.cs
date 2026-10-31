using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>
/// Pricing details for a product in the caller's currency.
/// </summary>
/// <remarks>
/// Kept so the storefront keeps rendering a number until it reads <see cref="ProductDto.Prices"/>
/// instead. Filled from the currency the caller is billed in; a value is <see langword="null"/>
/// when the product has no price for that cycle in that currency.
/// </remarks>
/// <param name="Monthly">Monthly price in the caller's currency, or null.</param>
/// <param name="Annual">Annual price in the caller's currency, or null.</param>
/// <param name="Quarterly">Quarterly price in the caller's currency, or null.</param>
/// <param name="SemiAnnual">Semi-annual price in the caller's currency, or null.</param>
/// <param name="Biennial">Biennial price in the caller's currency, or null.</param>
public sealed record ProductPricingDto(
    decimal? Monthly,
    decimal? Annual,
    decimal? Quarterly,
    decimal? SemiAnnual,
    decimal? Biennial)
{
    /// <summary>Builds the pricing by asking for each billing cycle in turn.</summary>
    /// <param name="amountFor">The amount of a cycle, or null when it is not sold.</param>
    /// <returns>The pricing, one optional amount per cycle.</returns>
    public static ProductPricingDto Of(Func<BillingCycle, decimal?> amountFor)
    {
        ArgumentNullException.ThrowIfNull(amountFor);
        return new(
            amountFor(BillingCycle.Monthly),
            amountFor(BillingCycle.Annual),
            amountFor(BillingCycle.Quarterly),
            amountFor(BillingCycle.SemiAnnual),
            amountFor(BillingCycle.Biennial));
    }
}
