namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Application.Billing.Extensions;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Extensions;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns a filtered list of products as DTOs, priced in the caller's currency.</summary>
/// <param name="repo">Product repository; every read includes the stored prices.</param>
/// <param name="payerCurrency">The currency the caller is billed in — a guest gets the base.</param>
/// <param name="currencies">The configured currencies, to validate an anonymous caller's requested code.</param>
/// <param name="caller">The current request's caller, to tell a guest from a signed-in client.</param>
public sealed class GetProductsHandler(
    IProductRepository repo,
    IPayerCurrencyResolver payerCurrency,
    ICurrencyRepository currencies,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="GetProductsQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Matching product DTOs. Products with no price in the caller's currency are omitted unless
    /// <see cref="GetProductsQuery.IncludeUnsellable"/> is set.
    /// </returns>
    /// <remarks>
    /// A signed-in client is always billed in their own recorded currency, so
    /// <see cref="GetProductsQuery.Currency"/> is only consulted for an anonymous caller — mirrors
    /// <c>GetTldPricingHandler</c>, whose target currency is likewise a guest-only concern; an
    /// unknown or disabled code is ignored rather than rejected, exactly like that TLD path.
    /// </remarks>
    public async Task<IReadOnlyList<ProductDto>> HandleAsync(GetProductsQuery qry, CancellationToken ct)
    {
        var currency = await ResolveCurrencyAsync(qry.Currency, ct);
        var products = await repo.ListAsync(qry.GroupId, qry.ActiveOnly, ct);

        return products
            .Where(p => qry.IncludeUnsellable || p.SellsIn(currency))
            .Select(p => p.ToDto(currency))
            .ToList();
    }

    /// <summary>
    /// Picks the currency to price this response in: an anonymous caller's explicit, enabled
    /// request wins; otherwise falls back to the caller's resolved billing currency (the client's
    /// own currency for a signed-in caller, the base for a guest with no valid request).
    /// </summary>
    /// <param name="requested">The <see cref="GetProductsQuery.Currency"/> value, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ISO 4217 alpha code to price in.</returns>
    private async Task<string> ResolveCurrencyAsync(string? requested, CancellationToken ct)
    {
        if (caller.UserId is null && !string.IsNullOrWhiteSpace(requested))
        {
            var offered = await currencies.FindOfferedAsync(requested, ct);
            if (offered is not null)
            {
                return offered.Code;
            }
        }

        return (await payerCurrency.ForCallerAsync(ct)).Code;
    }
}
