using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceChangeRequests;

/// <summary>
/// Returns a paginated list of plan change requests (admin view), newest first: the pending requests with
/// <paramref name="Status"/> set to <c>Pending</c>, or the plan change history of one service with
/// <paramref name="ServiceId"/> set.
/// </summary>
/// <param name="ServiceId">Optional filter: only the requests of this service.</param>
/// <param name="Status">Optional filter: only the requests in this status.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (max 100).</param>
public sealed record GetServiceChangeRequestsQuery(
    int? ServiceId = null, ServiceChangeStatus? Status = null, int Page = 1, int PageSize = 20);
