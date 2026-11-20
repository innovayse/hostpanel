using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.VatRates.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>Returns a page of VAT rates mapped to <see cref="VatRateDto"/>, narrowed by the search text on the country, the kind and the standing of the row in SQL ahead of paging.</summary>
public sealed class ListVatRatesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the standing of a row is judged by.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the standing of a row is judged by.</param>
    public ListVatRatesHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size, search and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of rows by country, then kind, then the latest start first. Never refuses.</returns>
    public async Task<Result<PaginatedList<VatRateDto>>> HandleAsync(ListVatRatesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = _context.VatRates.AsNoTracking();
        var filtered = rows.Search(query.Search, r => r.Country);

        if (query.Kind is { } kind)
        {
            filtered = filtered.Where(r => r.Kind == kind);
        }

        if (query.Status is { } status)
        {
            filtered = filtered.WithStatus(rows, status, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
        }

        var ordered = filtered
            .OrderBy(r => r.Country)
            .ThenBy(r => r.Kind)
            .ThenByDescending(r => r.ValidFrom)
            .ThenBy(r => r.Id);
        var totalCount = await ordered.CountAsync(ct);
        var rates = await ordered.Paginate(query.Page, query.PageSize).SelectDto().ToListAsync(ct);

        return Result.Success(new PaginatedList<VatRateDto>(rates, totalCount, query.Page, query.PageSize));
    }
}
