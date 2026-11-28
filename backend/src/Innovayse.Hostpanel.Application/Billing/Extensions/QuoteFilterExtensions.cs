using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a quote list narrows its quotes: the search text on the subject, the id and (when the caller names
/// them) the clients that match it, and the stage and validity filters, all in SQL ahead of ordering and paging, so
/// the admin list and a client's list cannot differ.
/// </summary>
public static class QuoteFilterExtensions
{
    /// <summary>
    /// Keeps the quotes the query's search text and filters name; a filter left empty keeps everything.
    /// </summary>
    /// <typeparam name="T">The quote list query.</typeparam>
    /// <param name="quotes">The quotes to narrow.</param>
    /// <param name="query">The search text and the quote filters.</param>
    /// <param name="now">The moment a quote's expiry date is compared with, to tell a valid one from an expired one.</param>
    /// <param name="clientsMatchingSearch">
    /// The clients the search text names, whose quotes match too; none when the list does not search by client.
    /// </param>
    /// <returns>The narrowed quotes.</returns>
    public static IQueryable<Quote> Matching<T>(
        this IQueryable<Quote> quotes, T query, DateTimeOffset now, IReadOnlyList<int> clientsMatchingSearch)
        where T : IListQuery, IQuoteListFilters
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(query);

        quotes = quotes.SearchIncludingIdOrMatching(
            query.Search, q => q.Id, q => clientsMatchingSearch.Contains(q.ClientId), q => q.Subject);

        if (query.Stage is { } stage)
        {
            quotes = quotes.Where(q => q.Stage == stage);
        }

        return query.Validity switch
        {
            QuoteValidity.Valid => quotes.Where(q => q.ExpiryDate >= now),
            QuoteValidity.Expired => quotes.Where(q => q.ExpiryDate < now),
            _ => quotes,
        };
    }
}
