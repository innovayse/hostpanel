using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a billable item list narrows its items: the search text on the description, the id and (when the caller names them) the clients that match it, and the type filter,
/// all in SQL ahead of ordering and paging, so the admin list and the list on a client's profile cannot differ.
/// </summary>
public static class BillableItemFilterExtensions
{
    /// <summary>Keeps the items the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The billable item list query.</typeparam>
    /// <param name="items">The billable items to narrow.</param>
    /// <param name="query">The search text and the billable item filters.</param>
    /// <param name="clientsMatchingSearch">The clients the search text names, whose items match too; <see langword="null"/> when the list does not search by client.</param>
    /// <returns>The narrowed items, in the order they came.</returns>
    public static IQueryable<BillableItem> Matching<T>(
        this IQueryable<BillableItem> items, T query, IReadOnlyList<int>? clientsMatchingSearch = null)
        where T : IListQuery, IBillableItemListFilters
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        var clients = clientsMatchingSearch ?? [];
        items = items.SearchIncludingIdOrMatching(query.Search, x => x.Id, x => clients.Contains(x.ClientId), x => x.Description);

        return query.Type is { } type ? items.Where(x => x.Type == type) : items;
    }
}
