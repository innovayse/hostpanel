using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one definition of "the rule for this place", shared by the uniqueness check of the rule use cases and by the
/// rate lookup, so a rule is found for the same country and region however it is asked for.
/// </summary>
public static class TaxRuleQueryExtensions
{
    /// <summary>Narrows the rules to one place: a country, and a region of it or the whole country.</summary>
    /// <param name="source">The rules to narrow.</param>
    /// <param name="country">The country code, as <see cref="TaxRule.Country"/> stores it (upper case).</param>
    /// <param name="region">The region, compared without regard to case, or null for the country-wide rule.</param>
    /// <returns>The rules of that place; at most one exists.</returns>
    public static IQueryable<TaxRule> ForPlace(this IQueryable<TaxRule> source, string country, string? region)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalizedRegion = string.IsNullOrWhiteSpace(region) ? null : region.Trim().ToLower();
        return normalizedRegion is null
            ? source.Where(r => r.Country == country && r.Region == null)
            : source.Where(r => r.Country == country && r.Region != null && r.Region.ToLower() == normalizedRegion);
    }
}
