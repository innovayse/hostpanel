using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProducts;

/// <summary>
/// Loads one page of the staff view of the products in the operator's order, narrowed by the search text, the group, the
/// type and the status in SQL ahead of paging, each priced in the caller's currency like the picker read
/// (<c>GetProductsHandler</c>) prices them.
/// </summary>
public sealed class ListProductsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The currency the caller is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">The currency the caller is billed in.</param>
    public ListProductsHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _payerCurrency = payerCurrency;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size, search and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of product DTOs, the unsellable ones included. Never refuses.</returns>
    public async Task<Result<PaginatedList<ProductDto>>> HandleAsync(ListProductsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currency = (await _payerCurrency.ForCallerAsync(ct)).Code;

        var ordered = _context.Products.AsNoTracking()
            .Search(query.Search, p => p.Name, p => p.Sku, p => p.Slug)
            .Matching(query.GroupId, query.Status, query.Type)
            .InDisplayOrder();
        var totalCount = await ordered.CountAsync(ct);
        var products = await ordered.Include(p => p.Prices).Paginate(query.Page, query.PageSize).ToListAsync(ct);

        return Result.Success(new PaginatedList<ProductDto>(
            products.Select(p => p.ToDto(currency)).ToList(), totalCount, query.Page, query.PageSize));
    }
}
