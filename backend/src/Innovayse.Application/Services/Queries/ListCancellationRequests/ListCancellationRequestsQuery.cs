namespace Innovayse.Application.Services.Queries.ListCancellationRequests;

/// <summary>Returns a paginated list of cancellation requests (admin view).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (max 100).</param>
public record ListCancellationRequestsQuery(int Page = 1, int PageSize = 20);
