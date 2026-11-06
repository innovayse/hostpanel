using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Reports.Extensions;

/// <summary>
/// Shared filtering for the Transactions report, used by both the paged view
/// (<c>Queries/TransactionsReport</c>) and its CSV export (<c>Queries/ExportTransactionsReport</c>)
/// so the two always answer with exactly the same rows.
/// </summary>
public static class TransactionReportFilterExtensions
{
    /// <summary>Applies the Transactions report's optional filters to a transaction query.</summary>
    /// <param name="transactions">The base transaction query.</param>
    /// <param name="dateFrom">Earliest transaction day to include, inclusive (UTC).</param>
    /// <param name="dateTo">Latest transaction day to include, inclusive (UTC): everything before the next day's midnight.</param>
    /// <param name="paymentMethod">
    /// Payment method to keep, matched exactly; ignored (every method kept) when
    /// <see langword="null"/> or blank.
    /// </param>
    /// <returns>The filtered query, not yet materialized.</returns>
    public static IQueryable<Transaction> ApplyReportFilters(
        this IQueryable<Transaction> transactions,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        // Credit-only ledger lines moved no cash, so the cash report does not list them.
        var query = transactions.Where(Transaction.MovedMoneyFilter);

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.StartOfDayUtc();
            query = query.Where(t => t.Date >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.EndOfDayExclusiveUtc();
            query = query.Where(t => t.Date < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            query = query.Where(t => t.PaymentMethod == paymentMethod);
        }

        return query;
    }
}
