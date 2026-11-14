using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientBillableItems;

/// <summary>
/// Query to list billable items (uninvoiced + paginated invoiced) for a client. The search and the type filter narrow
/// both lists; the page and page size cut the invoiced list only, because the uninvoiced items are always listed whole.
/// </summary>
/// <param name="ClientId">The client's primary key; overlaid from the route.</param>
/// <param name="Page">1-based page number of the invoiced items.</param>
/// <param name="PageSize">Invoiced items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only items whose description contains it (case-insensitive), or whose id it is when it is a whole
/// number, are listed. Blank lists all.
/// </param>
/// <param name="Type">Optional type filter, bound by name from the query string; <see langword="null"/> for every type.</param>
public sealed record ListClientBillableItemsQuery(
    int ClientId,
    int Page,
    int PageSize,
    string? Search = null,
    BillableItemType? Type = null) : IListQuery;
