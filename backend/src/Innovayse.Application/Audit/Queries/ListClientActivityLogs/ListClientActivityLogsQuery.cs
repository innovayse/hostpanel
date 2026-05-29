namespace Innovayse.Application.Audit.Queries.ListClientActivityLogs;

/// <summary>Query to retrieve a paginated, filtered list of activity log entries for a specific client.</summary>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of entries per page (clamped to 1–100).</param>
/// <param name="Date">Optional UTC date filter — matches entries on this day.</param>
/// <param name="AdminSearch">Optional partial match on admin name or email.</param>
/// <param name="Description">Optional partial match on description.</param>
/// <param name="IpAddress">Optional partial match on IP address.</param>
public record ListClientActivityLogsQuery(
    int ClientId,
    int Page,
    int PageSize,
    DateTimeOffset? Date,
    string? AdminSearch,
    string? Description,
    string? IpAddress);
