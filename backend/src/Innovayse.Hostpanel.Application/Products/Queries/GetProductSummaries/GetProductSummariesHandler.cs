using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductSummaries;

/// <summary>
/// Handles <see cref="GetProductSummariesQuery"/>: loads the named products with their prices in one
/// pass and prices each in the currency the caller is billed in, the same answer the storefront and
/// the invoice give.
/// </summary>
public sealed class GetProductSummariesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The currency the caller is billed in; every price in the answer is in it.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">The currency the caller is billed in; every price in the answer is in it.</param>
    public GetProductSummariesHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _payerCurrency = payerCurrency;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="query">The products to describe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One summary per product that exists, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ProductSummaryDto>>> HandleAsync(
        GetProductSummariesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ProductIds.Count == 0)
        {
            return Result<IReadOnlyList<ProductSummaryDto>>.Success([]);
        }

        var currency = (await _payerCurrency.ForCallerAsync(ct)).Code;
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => query.ProductIds.Contains(p.Id))
            .ToListAsync(ct);

        var summaries = products
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.Name,
                p.PriceFor(currency, BillingCycle.Monthly),
                p.PriceFor(currency, BillingCycle.Annual),
                p.ToFulfilmentDto()))
            .ToList();

        return Result<IReadOnlyList<ProductSummaryDto>>.Success(summaries);
    }
}
