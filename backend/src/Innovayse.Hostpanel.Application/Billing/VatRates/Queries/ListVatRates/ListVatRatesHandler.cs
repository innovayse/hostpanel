using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.VatRates.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>Returns a page of VAT rates mapped to <see cref="VatRateDto"/>, narrowed by the search text on the country in SQL ahead of paging.</summary>
public sealed class ListVatRatesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListVatRatesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size and search.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of rows by country, then kind, then the latest start first. Never refuses.</returns>
    public async Task<Result<PaginatedList<VatRateDto>>> HandleAsync(ListVatRatesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var ordered = _context.VatRates.AsNoTracking()
            .Search(query.Search, r => r.Country)
            .OrderBy(r => r.Country)
            .ThenBy(r => r.Kind)
            .ThenByDescending(r => r.ValidFrom)
            .ThenBy(r => r.Id);
        var totalCount = await ordered.CountAsync(ct);
        var rates = await ordered.Paginate(query.Page, query.PageSize).SelectDto().ToListAsync(ct);

        return Result.Success(new PaginatedList<VatRateDto>(rates, totalCount, query.Page, query.PageSize));
    }
}
