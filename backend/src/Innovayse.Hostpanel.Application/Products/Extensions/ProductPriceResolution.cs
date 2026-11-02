using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// Turns what an admin form sent, either the per-currency list or the two legacy single-currency
/// figures, into the one price list a product is written from.
/// </summary>
/// <remarks>
/// The pre-multi-currency admin form still posts <c>monthlyPrice</c>/<c>annualPrice</c> and no
/// <c>prices</c>. Until that form is replaced, those two figures are the product's prices in the
/// base currency, and only there: a save from that form replaces the base currency's monthly and
/// annual rows and leaves every other currency as it was. That form cannot name the quarterly,
/// semi-annual and biennial cycles, so those keep the prices the product already has in the base
/// currency instead of being dropped by a save that never mentioned them. Both the create and the
/// update handler go through here so the rule lives once.
/// </remarks>
public static class ProductPriceResolution
{
    /// <summary>Resolves the effective price list.</summary>
    /// <param name="payerCurrency">Resolves the base currency the legacy figures are in.</param>
    /// <param name="prices">The per-currency list, when the caller sent one.</param>
    /// <param name="legacyMonthly">The legacy monthly figure, when the caller sent that instead.</param>
    /// <param name="legacyAnnual">The legacy annual figure, when the caller sent that instead.</param>
    /// <param name="existing">The product being updated, or <see langword="null"/> when it is being created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <paramref name="prices"/>, replacing every currency, when present; otherwise one entry in
    /// the base currency built from the legacy figures, touching that currency alone, and an
    /// empty list that touches nothing when neither figure was sent.
    /// </returns>
    public static async Task<ResolvedPrices> ResolveProductPricesAsync(
        this IPayerCurrencyResolver payerCurrency,
        IReadOnlyList<ProductPriceDto>? prices,
        decimal? legacyMonthly,
        decimal? legacyAnnual,
        Product? existing,
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
            [ProductPriceDto.Of(baseCurrency.Code, cycle => cycle switch
            {
                BillingCycle.Monthly => legacyMonthly,
                BillingCycle.Annual => legacyAnnual,
                _ => existing?.PriceFor(baseCurrency.Code, cycle),
            })],
            ReplaceAllCurrencies: false,
            baseCurrency.Code);
    }
}
