using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListAnnouncements;

/// <summary>Query to retrieve a page of the announcements staff manage, narrowed by a search and the published state.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only announcements whose title or content contain it (case-insensitive) are listed. Blank lists all.</param>
/// <param name="IsPublished">Optional published state: true for the published announcements, false for the drafts; <see langword="null"/> for both.</param>
public sealed record ListAnnouncementsQuery(int Page, int PageSize, string? Search = null, bool? IsPublished = null) : IListQuery;
