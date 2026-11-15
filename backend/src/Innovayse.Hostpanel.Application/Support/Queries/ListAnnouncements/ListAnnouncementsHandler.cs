using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListAnnouncements;

/// <summary>Returns a page of the announcements, narrowed by the search text and the published state, newest first.</summary>
public sealed class ListAnnouncementsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListAnnouncementsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the published state and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the page. This use case answers no refusal code.</returns>
    public async Task<Result<PaginatedList<AnnouncementDto>>> HandleAsync(ListAnnouncementsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var announcements = _context.Announcements
            .AsNoTracking()
            .Search(query.Search, a => a.Title, a => a.Content);

        if (query.IsPublished is { } isPublished)
        {
            announcements = announcements.Where(a => a.IsPublished == isPublished);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = announcements.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);
        var totalCount = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .SelectDto()
            .ToListAsync(ct);

        return Result.Success(new PaginatedList<AnnouncementDto>(items, totalCount, query.Page, query.PageSize));
    }
}
