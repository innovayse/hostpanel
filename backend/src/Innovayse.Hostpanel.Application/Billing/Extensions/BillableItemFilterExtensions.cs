using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way a billable item list narrows its items: the search text on the description and the id, and the type filter,
/// all in SQL ahead of ordering and paging, so the admin list and the list on a client's profile cannot differ.
/// </summary>
public static class BillableItemFilterExtensions
{
    /// <summary>Keeps the items the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The billable item list query.</typeparam>
    /// <param name="items">The billable items to narrow.</param>
    /// <param name="query">The search text and the billable item filters.</param>
    /// <returns>The narrowed items, in the order they came.</returns>
    public static IQueryable<BillableItem> Matching<T>(this IQueryable<BillableItem> items, T query)
        where T : IListQuery, IBillableItemListFilters
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        items = items.SearchIncludingId(query.Search, x => x.Id, x => x.Description);

        return query.Type is { } type ? items.Where(x => x.Type == type) : items;
    }
}
