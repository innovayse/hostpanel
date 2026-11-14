using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;

/// <summary>
/// Returns a page of tax rules mapped to <see cref="TaxRuleDto"/>, narrowed by the search text on the name, country and
/// region and by the enabled state in SQL ahead of paging.
/// </summary>
public sealed class ListTaxRulesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListTaxRulesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size, search and enabled filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of rules, by country, then region (the country-wide rule first). Never refuses.</returns>
    public async Task<Result<PaginatedList<TaxRuleDto>>> HandleAsync(ListTaxRulesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.TaxRules.AsNoTracking().Search(query.Search, r => r.Name, r => r.Country, r => r.Region);

        if (query.Enabled is { } enabled)
        {
            filtered = filtered.Where(r => r.IsEnabled == enabled);
        }

        var ordered = filtered
            .OrderBy(r => r.Country)
            // A missing region sorts last in PostgreSQL, so order on its presence to put the country-wide rule first.
            .ThenBy(r => r.Region != null)
            .ThenBy(r => r.Region)
            .ThenBy(r => r.Id);
        var totalCount = await ordered.CountAsync(ct);
        var rules = await ordered.Paginate(query.Page, query.PageSize).SelectDto().ToListAsync(ct);

        return Result.Success(new PaginatedList<TaxRuleDto>(rules, totalCount, query.Page, query.PageSize));
    }
}
