using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListClientEmailLogs;

/// <summary>Returns a page of email logs sent to a specific client, narrowed by the search text and the filters.</summary>
public sealed class ListClientEmailLogsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to resolve the client's sign-in address through the Clients module; a log entry is keyed by address, not by id.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to resolve the client's sign-in address through the Clients module.</param>
    public ListClientEmailLogsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client, the search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The client's page of log entries, newest first, or an empty page when the client id names
    /// nobody, never a refusal.
    /// </returns>
    public async Task<Result<PaginatedList<EmailLogDto>>> HandleAsync(
        ListClientEmailLogsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _bus.FindClientBriefAsync(query.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return Result<PaginatedList<EmailLogDto>>.Success(new PaginatedList<EmailLogDto>([], 0, query.Page, query.PageSize));
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = _context.EmailLogs
            .AsNoTracking()
            .Where(l => l.To == email)
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
