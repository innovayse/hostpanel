using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProducts;

/// <summary>Handles <see cref="GetOrderableProductsQuery"/>: reads the named products and their prices in one pass.</summary>
public sealed class GetOrderableProductsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetOrderableProductsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The products and the currency to price them in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per product that exists, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<OrderableProductDto>>> HandleAsync(
        GetOrderableProductsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ProductIds.Count == 0)
        {
            return Result<IReadOnlyList<OrderableProductDto>>.Success([]);
        }

        var currency = query.Currency.ToUpperInvariant();
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => query.ProductIds.Contains(p.Id))
            .ToListAsync(ct);

        var orderable = products
            .Select(p => new OrderableProductDto(
                p.Id,
                p.Name,
                p.Status,
                new ProductPriceDto(
                    currency,
                    p.PriceFor(currency, BillingCycle.Monthly),
                    p.PriceFor(currency, BillingCycle.Annual)),
                p.ToFulfilmentDto()))
            .ToList();

        return Result<IReadOnlyList<OrderableProductDto>>.Success(orderable);
    }
}
