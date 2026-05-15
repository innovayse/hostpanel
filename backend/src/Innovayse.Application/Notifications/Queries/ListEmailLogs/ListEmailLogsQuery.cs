namespace Innovayse.Application.Notifications.Queries.ListEmailLogs;

/// <summary>Query to retrieve a paged list of email log entries.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of entries per page.</param>
public record ListEmailLogsQuery(int Page, int PageSize);
