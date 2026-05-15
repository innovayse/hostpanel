namespace Innovayse.Application.Services.Queries.GetServices;

/// <summary>Returns a paginated list of all client services (admin view).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (max 100).</param>
/// <param name="ClientId">Optional client ID filter.</param>
public record GetServicesQuery(int Page = 1, int PageSize = 20, int? ClientId = null);
