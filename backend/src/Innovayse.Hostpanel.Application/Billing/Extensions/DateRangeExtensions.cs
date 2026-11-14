using System.Linq.Expressions;
using Innovayse.Hostpanel.Application.Common.Extensions;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a billing list narrows its rows to the calendar days typed into its filter. The days are bare dates, turned
/// into UTC instants by <see cref="DateOnlyExtensions"/> exactly as the reports do, so the range is the half-open window
/// <c>[from 00:00, to + 1 day 00:00)</c> whatever zone the host runs in, and a range of one day lists that whole day. The
/// invoice and transaction lists, the two that offer a "from" and a "to", agree on what "to" means because they both use this.
/// </summary>
public static class DateRangeExtensions
{
    /// <summary>Keeps the rows whose moment falls on one of the days from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The rows to narrow.</param>
    /// <param name="moment">Selects the moment the period is compared with (for example the creation moment).</param>
    /// <param name="from">The first day kept; <see langword="null"/> for no lower bound.</param>
    /// <param name="to">The last day kept, the whole day included; <see langword="null"/> for no upper bound.</param>
    /// <returns>The narrowed rows; both bounds compare the column itself, with no function over it, so an index can serve the range.</returns>
    public static IQueryable<T> InPeriod<T>(
        this IQueryable<T> source, Expression<Func<T, DateTimeOffset>> moment, DateOnly? from, DateOnly? to)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(moment);

        if (from is { } firstDay)
        {
            var start = firstDay.StartOfDayUtc();

            // A closure member, not a constant, so the provider sends the bound as a parameter.
            Expression<Func<DateTimeOffset>> lower = () => start;
            source = source.Where(Expression.Lambda<Func<T, bool>>(
                Expression.GreaterThanOrEqual(moment.Body, lower.Body), moment.Parameters));
        }

        // The last representable day has no day after it, so it needs no upper bound.
        if (to is { } lastDay && lastDay < DateOnly.MaxValue)
        {
            var exclusiveEnd = lastDay.EndOfDayExclusiveUtc();
            Expression<Func<DateTimeOffset>> upper = () => exclusiveEnd;
            source = source.Where(Expression.Lambda<Func<T, bool>>(
                Expression.LessThan(moment.Body, upper.Body), moment.Parameters));
        }

        return source;
    }
}
