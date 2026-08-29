namespace Innovayse.Application.Support.Queries.ListPublishedAnnouncements;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paged list of published announcements, newest first, for the client portal.
/// </summary>
/// <remarks>
/// The published filter is applied by the repository, inside the same query that pages, so the
/// page numbers and <c>TotalCount</c> describe the rows a client can actually see. Filtering a
/// page after it had been fetched would leave drafts occupying slots and make pages look short
/// for no visible reason.
/// </remarks>
/// <param name="repo">Announcement persistence.</param>
public sealed class ListPublishedAnnouncementsHandler(IAnnouncementRepository repo)
{
    /// <summary>Handles <see cref="ListPublishedAnnouncementsQuery"/>.</summary>
    /// <param name="query">The query with pagination. It carries no visibility switch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result of published announcements.</returns>
    public async Task<PagedResult<PublishedAnnouncementDto>> HandleAsync(
        ListPublishedAnnouncementsQuery query,
        CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (announcements, totalCount) = await repo.ListPublishedAsync(page, pageSize, ct);

        var items = announcements
            .Select(a => new PublishedAnnouncementDto(a.Id, a.Title, a.Content, a.CreatedAt))
            .ToList();

        return new PagedResult<PublishedAnnouncementDto>(items, totalCount, page, pageSize);
    }
}
