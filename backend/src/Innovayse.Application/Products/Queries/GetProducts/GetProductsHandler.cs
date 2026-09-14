namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Products.Extensions;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns a filtered list of products as DTOs, priced in the caller's currency.</summary>
/// <param name="repo">Product repository; every read includes the stored prices.</param>
/// <param name="payerCurrency">The currency the caller is billed in — a guest gets the base.</param>
public sealed class GetProductsHandler(IProductRepository repo, IPayerCurrencyResolver payerCurrency)
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
    public async Task<IReadOnlyList<ProductDto>> HandleAsync(GetProductsQuery qry, CancellationToken ct)
    {
        var currency = (await payerCurrency.ForCallerAsync(ct)).Code;
        var products = await repo.ListAsync(qry.GroupId, qry.ActiveOnly, ct);

        return products
            .Where(p => qry.IncludeUnsellable || p.SellsIn(currency))
            .Select(p => p.ToDto(currency))
            .ToList();
    }
}
