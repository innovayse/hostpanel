namespace Innovayse.Application.Products.Common;

using Innovayse.Application.Billing.Interfaces;

/// <summary>
/// Turns what an admin form sent — either the per-currency list or the two legacy single-currency
/// figures — into the one price list a product is written from.
/// </summary>
/// <remarks>
/// The pre-multi-currency admin form still posts <c>monthlyPrice</c>/<c>annualPrice</c> and no
/// <c>prices</c>. Until that form is replaced, those two figures are the product's prices in the
/// base currency — and only there: a save from that form replaces the base currency's rows and
/// leaves every other currency as it was. Both the create and the update handler go through here
/// so the rule lives once.
/// </remarks>
public static class ProductPriceInputs
{
    /// <summary>Resolves the effective price list.</summary>
    /// <param name="prices">The per-currency list, when the caller sent one.</param>
    /// <param name="legacyMonthly">The legacy monthly figure, when the caller sent that instead.</param>
    /// <param name="legacyAnnual">The legacy annual figure, when the caller sent that instead.</param>
    /// <param name="payerCurrency">Resolves the base currency the legacy figures are in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <paramref name="prices"/>, replacing every currency, when present; otherwise one entry in
    /// the base currency built from the legacy figures, touching that currency alone — and an
    /// empty list that touches nothing when neither figure was sent.
    /// </returns>
    public static async Task<ResolvedPrices> ResolveAsync(
        IReadOnlyList<ProductPriceInput>? prices,
        decimal? legacyMonthly,
        decimal? legacyAnnual,
        IPayerCurrencyResolver payerCurrency,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payerCurrency);

        var baseCurrency = await payerCurrency.BaseAsync(ct);

        if (prices is not null)
        {
            return new ResolvedPrices(prices, ReplaceAllCurrencies: true, baseCurrency.Code);
        }

        if (legacyMonthly is null && legacyAnnual is null)
        {
            return new ResolvedPrices([], ReplaceAllCurrencies: false, baseCurrency.Code);
        }

        return new ResolvedPrices(
            [new ProductPriceInput(baseCurrency.Code, legacyMonthly, legacyAnnual)],
            ReplaceAllCurrencies: false,
            baseCurrency.Code);
    }
}
