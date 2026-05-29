namespace Innovayse.Application.Support.Queries.ListAnnouncements;

using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paged list of announcements ordered by creation date descending.
/// Maps results to <see cref="AnnouncementDto"/>.
/// </summary>
public sealed class ListAnnouncementsHandler(IAnnouncementRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListAnnouncementsQuery"/>.
    /// </summary>
    /// <param name="query">The list announcements query with pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing announcement DTOs.</returns>
    public async Task<PagedResult<AnnouncementDto>> HandleAsync(ListAnnouncementsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (announcements, totalCount) = await repo.ListAsync(page, pageSize, ct);

        var items = announcements
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Content, a.IsPublished, a.CreatedAt))
            .ToList();

        return new PagedResult<AnnouncementDto>(items, totalCount, page, pageSize);
    }
}
