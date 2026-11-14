using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Audit.Queries.ListClientActivityLogs;

/// <summary>Query to retrieve a page of the activity log entries of one client, narrowed by a search and a period.</summary>
/// <param name="ClientId">FK to the client; the controller overlays it from the route.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only entries whose admin name, admin email, description or IP address contain it (case-insensitive, a
/// <c>%</c> or <c>_</c> matches itself) are listed. Blank lists all.
/// </param>
/// <param name="From">Optional first day kept (UTC), bound from <c>yyyy-MM-dd</c>; <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last day kept (UTC), bound from <c>yyyy-MM-dd</c>, the whole day included; <see langword="null"/> for no upper bound.</param>
public sealed record ListClientActivityLogsQuery(
    int ClientId,
    int Page,
    int PageSize,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null) : IListQuery, IPeriodListFilter;
