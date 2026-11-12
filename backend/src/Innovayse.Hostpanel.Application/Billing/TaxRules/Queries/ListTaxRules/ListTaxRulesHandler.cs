using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;

/// <summary>Returns every tax rule mapped to <see cref="TaxRuleDto"/>.</summary>
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
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every rule, by country, then region (the country-wide rule first). Never refuses.</returns>
    public async Task<Result<IReadOnlyList<TaxRuleDto>>> HandleAsync(ListTaxRulesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rules = await _context.TaxRules
            .AsNoTracking()
            .OrderBy(r => r.Country)
            .ThenBy(r => r.Region)
            .ThenBy(r => r.Id)
            .SelectDto()
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<TaxRuleDto>>(rules);
    }
}
