using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The two definitions of "which VAT rate row", shared by the use cases that write rows, the startup import and the rate
/// lookup, so a row is found the same way however it is asked for.
/// </summary>
public static class VatRateQueryExtensions
{
    /// <summary>Narrows the rows to the one a country, kind and first day identify; at most one exists.</summary>
    /// <param name="source">The rows to narrow.</param>
    /// <param name="country">The country code, as <see cref="VatRate.Country"/> stores it (upper case).</param>
    /// <param name="kind">The kind of rate.</param>
    /// <param name="validFrom">The first day the rate applies.</param>
    /// <returns>The matching rows.</returns>
    public static IQueryable<VatRate> ForKey(this IQueryable<VatRate> source, string country, VatRateKind kind, DateOnly validFrom)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Where(r => r.Country == country && r.Kind == kind && r.ValidFrom == validFrom);
    }

    /// <summary>Narrows the rows to the rate of a country and kind in force on a day: the one with the latest first day that is not after it.</summary>
    /// <param name="source">The rows to narrow.</param>
    /// <param name="country">The country code, as <see cref="VatRate.Country"/> stores it (upper case).</param>
    /// <param name="kind">The kind of rate.</param>
    /// <param name="day">The day the rate is wanted for.</param>
    /// <returns>The country's rows of that kind in force on the day, the one in force first; none when the country had no rate yet.</returns>
    public static IQueryable<VatRate> InForceOn(this IQueryable<VatRate> source, string country, VatRateKind kind, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .Where(r => r.Country == country && r.Kind == kind && r.ValidFrom <= day)
            .OrderByDescending(r => r.ValidFrom);
    }
}
