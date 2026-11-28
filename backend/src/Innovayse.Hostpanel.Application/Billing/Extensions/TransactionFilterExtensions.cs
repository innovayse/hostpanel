using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a transaction list narrows its transactions: the search text (on the id, the description, the reference, the payment
/// method and, when the caller names them, the clients that match it), the client, the direction, the payment
/// method, the period and the amount, all in SQL ahead of ordering, paging and the summary totals, so the list, its
/// count and its totals always describe the same rows.
/// </summary>
public static class TransactionFilterExtensions
{
    /// <summary>Keeps the transactions the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The transaction list query.</typeparam>
    /// <param name="transactions">The transactions to narrow.</param>
    /// <param name="query">The search text and the transaction filters.</param>
    /// <param name="clientsMatchingSearch">The clients the search text names, whose transactions match too; <see langword="null"/> when the list does not search by client.</param>
    /// <returns>The narrowed transactions.</returns>
    public static IQueryable<Transaction> Matching<T>(
        this IQueryable<Transaction> transactions, T query, IReadOnlyList<int>? clientsMatchingSearch = null)
        where T : IListQuery, ITransactionListFilters
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(query);

        var clients = clientsMatchingSearch ?? [];
        transactions = transactions.SearchIncludingIdOrMatching(
            query.Search,
            t => t.Id,
            t => clients.Contains(t.ClientId),
            t => t.Description,
            t => t.TransactionId,
            t => t.PaymentMethod);

        if (query.ClientId is { } clientId)
        {
            transactions = transactions.Where(t => t.ClientId == clientId);
        }

        transactions = query.Direction switch
        {
            TransactionDirection.In => transactions.Where(t => t.AmountIn > 0),
            TransactionDirection.Out => transactions.Where(t => t.AmountOut > 0),
            _ => transactions,
        };

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
        {
            var method = query.PaymentMethod;
            transactions = transactions.Where(t => t.PaymentMethod == method);
        }

        transactions = transactions.InPeriod(t => t.Date, query.From, query.To);

        if (query.Amount is { } amount)
        {
            transactions = transactions.Where(t => t.AmountIn == amount || t.AmountOut == amount);
        }

        return transactions;
    }
}
