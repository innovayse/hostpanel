using System.Linq.Expressions;
using Innovayse.Hostpanel.Application.Billing.VatRates.Common.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The definitions of "which VAT rate row", shared by the use cases that write rows, the startup import and the rate
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

    /// <summary>Narrows the rows to those standing as asked on a day, among the rows of their own country and kind.</summary>
    /// <param name="source">The rows to narrow, already narrowed by whatever else the caller filters on.</param>
    /// <param name="allRows">Every row of the table: whether a later row has started is asked of all of them, never of the narrowed set.</param>
    /// <param name="status">Whether to keep the rows in force, the ones a later row has replaced, or the ones not yet started.</param>
    /// <param name="day">The day the standing is judged on.</param>
    /// <returns>The rows with that standing.</returns>
    public static IQueryable<VatRate> WithStatus(
        this IQueryable<VatRate> source, IQueryable<VatRate> allRows, VatRateStatus status, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(allRows);

        var replaced = ReplacedOn(allRows, day);
        return status switch
        {
            VatRateStatus.Upcoming => source.Where(r => r.ValidFrom > day),
            VatRateStatus.Superseded => source.Where(r => r.ValidFrom <= day).Where(replaced),
            VatRateStatus.Active => source.Where(r => r.ValidFrom <= day).Where(Not(replaced)),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown VAT rate status."),
        };
    }

    /// <summary>Builds the one definition of "a later row of the same country and kind has started" that the status filter uses for both arms.</summary>
    /// <param name="allRows">Every row of the table.</param>
    /// <param name="day">The day the standing is judged on.</param>
    /// <returns>A predicate that is true for a row that a later row has replaced on the day.</returns>
    private static Expression<Func<VatRate, bool>> ReplacedOn(IQueryable<VatRate> allRows, DateOnly day) =>
        r => allRows.Any(later => later.Country == r.Country && later.Kind == r.Kind && later.ValidFrom > r.ValidFrom && later.ValidFrom <= day);

    /// <summary>Negates a row predicate so it can still be translated to SQL.</summary>
    /// <param name="predicate">The predicate to negate.</param>
    /// <returns>A predicate true exactly where <paramref name="predicate"/> is false.</returns>
    private static Expression<Func<VatRate, bool>> Not(Expression<Func<VatRate, bool>> predicate) =>
        Expression.Lambda<Func<VatRate, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

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
