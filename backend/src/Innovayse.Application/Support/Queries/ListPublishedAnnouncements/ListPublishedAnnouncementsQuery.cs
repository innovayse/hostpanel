namespace Innovayse.Application.Support.Queries.ListPublishedAnnouncements;

/// <summary>Query to retrieve a paginated list of the announcements clients may read.</summary>
/// <remarks>
/// Carries no "include unpublished" switch. Which rows are visible is settled by the message
/// type itself, not by a flag a caller could flip, so nothing dispatched through here can widen
/// its own result set. The admin read that returns every row, drafts included, is a separate use
/// case: <c>ListAnnouncementsQuery</c>.
/// </remarks>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListPublishedAnnouncementsQuery(int Page, int PageSize);
