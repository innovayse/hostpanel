namespace Innovayse.Application.Notifications.Queries.ListClientEmailLogs;

/// <summary>Query to retrieve a paginated list of email logs sent to a specific client.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of entries per page (clamped to 1–100).</param>
public record ListClientEmailLogsQuery(int ClientId, int Page, int PageSize);
