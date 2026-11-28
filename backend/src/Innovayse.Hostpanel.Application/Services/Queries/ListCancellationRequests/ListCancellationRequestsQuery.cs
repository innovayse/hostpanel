using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListCancellationRequests;

/// <summary>Returns a paginated, optionally searched and filtered list of cancellation requests (admin view).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only requests whose reason contains it (case-insensitive), or whose id it is when it is a whole number, or whose
/// service (its own domain, linked domain, product name, client name, company or sign-in email, or id) matches, are listed. Blank lists all.
/// </param>
/// <param name="Type">Optional type filter, bound by name from the query string; <see langword="null"/> for every type.</param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
public sealed record ListCancellationRequestsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    CancellationType? Type = null,
    CancellationStatus? Status = null) : IListQuery;
