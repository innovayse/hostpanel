using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListEmailLogs;

/// <summary>Returns a page of the platform's email log, narrowed by the search text and the filters, newest first.</summary>
public sealed class ListEmailLogsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListEmailLogsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of log entries, newest first. Never refuses.</returns>
    public async Task<Result<PaginatedList<EmailLogDto>>> HandleAsync(
        ListEmailLogsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = _context.EmailLogs
            .AsNoTracking()
            .Matching(query)
            .OrderByDescending(l => l.SentAt)
            .ThenByDescending(l => l.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .SelectDto()
            .ToListAsync(ct);

        return Result<PaginatedList<EmailLogDto>>.Success(new PaginatedList<EmailLogDto>(items, total, query.Page, query.PageSize));
    }
}
