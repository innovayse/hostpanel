namespace Innovayse.Application.Support.Queries.ListAnnouncements;

/// <summary>Query to retrieve a paginated list of announcements.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListAnnouncementsQuery(int Page, int PageSize);
