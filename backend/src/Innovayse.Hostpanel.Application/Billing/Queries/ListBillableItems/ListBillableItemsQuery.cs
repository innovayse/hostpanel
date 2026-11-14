using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListBillableItems;

/// <summary>Query to retrieve a paged, optionally searched and filtered list of the billable items of every client.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only items whose description contains it (case-insensitive), or whose id it is when it is a whole
/// number, are listed. Blank lists all. The client's name is not searched: only the Clients module can read it, so a
/// client is chosen with <paramref name="ClientId"/>.
/// </param>
/// <param name="ClientId">Optional client filter; <see langword="null"/> for every client.</param>
/// <param name="Type">Optional type filter, bound by name from the query string; <see langword="null"/> for every type.</param>
/// <param name="Invoiced">Optional invoiced filter: <see langword="true"/> keeps invoiced items, <see langword="false"/> uninvoiced ones, <see langword="null"/> both.</param>
public sealed record ListBillableItemsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? ClientId = null,
    BillableItemType? Type = null,
    bool? Invoiced = null) : IListQuery;
