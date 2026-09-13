namespace Innovayse.Application.Products.Common;

using Innovayse.Application.Billing.Interfaces;

/// <summary>
/// Turns what an admin form sent — either the per-currency list or the two legacy single-currency
/// figures — into the one list of prices a product is written from.
/// </summary>
/// <remarks>
/// The pre-multi-currency admin form still posts <c>monthlyPrice</c>/<c>annualPrice</c> and no
/// <c>prices</c>. Until that form is replaced, those two figures are the product's prices in the
/// base currency. Both the create and the update handler go through here so the rule lives once.
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
    /// <paramref name="prices"/> when present; otherwise one entry in the base currency built from
    /// the legacy figures — empty when neither figure was sent.
    /// </returns>
    public static async Task<IReadOnlyList<ProductPriceInput>> ResolveAsync(
        IReadOnlyList<ProductPriceInput>? prices,
        decimal? legacyMonthly,
        decimal? legacyAnnual,
        IPayerCurrencyResolver payerCurrency,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payerCurrency);

        if (prices is not null)
        {
            return prices;
        }

        if (legacyMonthly is null && legacyAnnual is null)
        {
            return [];
        }

        var baseCurrency = await payerCurrency.BaseAsync(ct);
        return [new ProductPriceInput(baseCurrency.Code, legacyMonthly, legacyAnnual)];
    }
}
