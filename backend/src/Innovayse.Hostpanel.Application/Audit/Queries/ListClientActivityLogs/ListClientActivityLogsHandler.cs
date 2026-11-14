using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Audit.Queries.ListClientActivityLogs;

/// <summary>Returns a page of the activity log entries of one client, narrowed by the search text and the period.</summary>
public sealed class ListClientActivityLogsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListClientActivityLogsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles <see cref="ListClientActivityLogsQuery"/>.</summary>
    /// <param name="query">The client, the search, the period and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the page of activity log entries, newest first. A client id that names nobody simply
    /// answers an empty page; this use case refuses nothing.
    /// </returns>
    public async Task<Result<PaginatedList<ActivityLogDto>>> HandleAsync(
        ListClientActivityLogsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Read-only, so no tracking is needed.
        var logs = _context.ActivityLogs
            .AsNoTracking()
            .Where(l => l.ClientId == query.ClientId)
            .Search(query.Search, l => l.AdminName, l => l.AdminEmail, l => l.Description, l => l.IpAddress)
            .InPeriod(l => l.CreatedAt, query.From, query.To);

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = logs.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .Select(l => new ActivityLogDto(l.Id, l.Description, l.AdminName, l.AdminEmail, l.IpAddress, l.CreatedAt))
            .ToListAsync(ct);

        return Result<PaginatedList<ActivityLogDto>>.Success(new PaginatedList<ActivityLogDto>(items, total, query.Page, query.PageSize));
    }
}
