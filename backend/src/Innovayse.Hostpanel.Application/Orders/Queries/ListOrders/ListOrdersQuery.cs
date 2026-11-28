using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Orders.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Queries.ListOrders;

/// <summary>Query to retrieve a paged, optionally searched and filtered list of all orders.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only orders whose number contains it (case-insensitive), or whose id it is when it is a whole number, or whose
/// client (name, company, sign-in email) matches, are listed. Blank lists all.
/// </param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
public sealed record ListOrdersQuery(
    int Page,
    int PageSize,
    string? Search = null,
    OrderStatus? Status = null) : IListQuery;
