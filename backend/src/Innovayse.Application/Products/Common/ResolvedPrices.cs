namespace Innovayse.Application.Products.Common;

/// <summary>
/// The price list an admin form resolved to, together with how far it reaches: the whole
/// product, or only the base currency.
/// </summary>
/// <remarks>
/// The per-currency list is the whole truth — a currency absent from it is one the product no
/// longer sells in. The legacy single-currency pair only ever spoke about the base currency, so a
/// form that still posts it must not wipe the other currencies an admin priced elsewhere.
/// </remarks>
/// <param name="Prices">The prices to write, one entry per currency.</param>
/// <param name="ReplaceAllCurrencies">
/// <see langword="true"/> when a currency absent from <paramref name="Prices"/> is removed from
/// the product; <see langword="false"/> when only the currencies listed are touched.
/// </param>
/// <param name="BaseCurrencyCode">ISO 4217 alpha code of the base currency, upper case.</param>
public sealed record ResolvedPrices(
    IReadOnlyList<ProductPriceInput> Prices,
    bool ReplaceAllCurrencies,
    string BaseCurrencyCode)
{
    /// <summary>Amount written to a legacy price column when the list has no base-currency figure for it.</summary>
    private const decimal NoPrice = 0m;

    /// <summary>
    /// Gets the base-currency monthly price, or <see cref="NoPrice"/> when the list has none. This
    /// is what the legacy <c>MonthlyPrice</c> column is written from.
    /// </summary>
    public decimal BaseMonthly => BaseEntry?.Monthly ?? NoPrice;

    /// <summary>
    /// Gets the base-currency annual price, or <see cref="NoPrice"/> when the list has none. This
    /// is what the legacy <c>AnnualPrice</c> column is written from.
    /// </summary>
    public decimal BaseAnnual => BaseEntry?.Annual ?? NoPrice;

    /// <summary>Gets the list entry for the base currency, if the admin priced it.</summary>
    private ProductPriceInput? BaseEntry =>
        Prices.FirstOrDefault(p => string.Equals(p.CurrencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase));
}
