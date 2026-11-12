using Innovayse.Hostpanel.Application.Billing.TaxRules.Common;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Maps <see cref="TaxRule"/> to <see cref="TaxRuleDto"/>. The list and the single read both project through
/// this one mapping, so a field added to the DTO is added once.
/// </summary>
public static class TaxRuleMappingExtensions
{
    /// <summary>Projects the query to <see cref="TaxRuleDto"/> rows.</summary>
    /// <param name="source">The rules to project.</param>
    /// <returns>The query projected to <see cref="TaxRuleDto"/>.</returns>
    public static IQueryable<TaxRuleDto> SelectDto(this IQueryable<TaxRule> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(r => new TaxRuleDto(r.Id, r.Name, r.Country, r.Region, r.RatePercent, r.IsEnabled));
    }
}
