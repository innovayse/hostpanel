using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProducts;

/// <summary>
/// Returns the staff view of the products as DTOs, priced in the caller's currency. The storefront
/// reads the catalogue through <c>ListCatalogProductsHandler</c> instead, which carries no
/// provisioning field and never lists an inactive product.
/// </summary>
public sealed class GetProductsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The currency the caller is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">The currency the caller is billed in.</param>
    public GetProductsHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _payerCurrency = payerCurrency;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">The filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Matching product DTOs. Products with no price in the caller's currency are omitted unless
    /// <see cref="GetProductsQuery.IncludeUnsellable"/> is set. Never refuses.
    /// </returns>
    public async Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(GetProductsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var currency = (await _payerCurrency.ForCallerAsync(ct)).Code;

        var products = await _context.Products.AsNoTracking().Include(p => p.Prices)
            .Matching(qry.GroupId, qry.ActiveOnly ? ProductStatus.Active : null)
            .InDisplayOrder()
            .ToListAsync(ct);

        var dtos = products
            .Where(p => qry.IncludeUnsellable || p.SellsIn(currency))
            .Select(p => p.ToDto(currency))
            .ToList();

        return Result<IReadOnlyList<ProductDto>>.Success(dtos);
    }
}
