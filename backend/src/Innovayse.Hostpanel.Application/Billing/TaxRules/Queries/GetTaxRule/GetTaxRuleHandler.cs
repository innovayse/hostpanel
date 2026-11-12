using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.GetTaxRule;

/// <summary>Loads a single tax rule and maps it to <see cref="TaxRuleDto"/>.</summary>
public sealed class GetTaxRuleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetTaxRuleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The rule id to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rule, or <c>TAX_RULE_NOT_FOUND</c> when the id names no rule.</returns>
    public async Task<Result<TaxRuleDto>> HandleAsync(GetTaxRuleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rule = await _context.TaxRules
            .AsNoTracking()
            .Where(r => r.Id == query.Id)
            .SelectDto()
            .FirstOrDefaultAsync(ct);
        if (rule is null)
        {
            return Result<TaxRuleDto>.FailureCode(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
        }

        return Result<TaxRuleDto>.Success(rule);
    }
}
