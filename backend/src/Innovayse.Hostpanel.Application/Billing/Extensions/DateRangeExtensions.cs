using System.Linq.Expressions;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a billing list narrows its rows to a date range typed into its filter: the first day is included from its
/// first instant and the last day is included to its last instant, so a range of one day lists that whole day. Every billing
/// list that offers a "from" and a "to" uses this, so the invoice, credit note, quote and transaction lists agree on what
/// "to" means.
/// </summary>
public static class DateRangeExtensions
{
    /// <summary>Keeps the rows whose moment falls inside the period, the day of <paramref name="to"/> included to its end.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The rows to narrow.</param>
    /// <param name="moment">Selects the moment the period is compared with (for example the creation moment).</param>
    /// <param name="from">The first moment kept, inclusive; <see langword="null"/> for no lower bound.</param>
    /// <param name="to">Any moment of the last day kept (UTC); the whole day is kept. <see langword="null"/> for no upper bound.</param>
    /// <returns>The narrowed rows; both bounds compare the column itself, with no function over it, so an index can serve the range.</returns>
    public static IQueryable<T> InPeriod<T>(
        this IQueryable<T> source, Expression<Func<T, DateTimeOffset>> moment, DateTimeOffset? from, DateTimeOffset? to)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(moment);

        if (from is { } start)
        {
            // A closure member, not a constant, so the provider sends the bound as a parameter.
            Expression<Func<DateTimeOffset>> lower = () => start;
            source = source.Where(Expression.Lambda<Func<T, bool>>(
                Expression.GreaterThanOrEqual(moment.Body, lower.Body), moment.Parameters));
        }

        // The last representable day has no day after it, so it needs no upper bound.
        if (to is { } end && end.UtcDateTime.Date < DateTime.MaxValue.Date)
        {
            // The day after the last day, at its first instant: the half-open end that keeps the last second of the day.
            var exclusiveEnd = new DateTimeOffset(end.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
            Expression<Func<DateTimeOffset>> upper = () => exclusiveEnd;
            source = source.Where(Expression.Lambda<Func<T, bool>>(
                Expression.LessThan(moment.Body, upper.Body), moment.Parameters));
        }

        return source;
    }
}
