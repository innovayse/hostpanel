using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListTickets;

/// <summary>
/// Returns a paged list of support tickets, narrowed by the search text on the subject and the ticket id and by status, priority,
/// department and flag, all in SQL ahead of paging, mapped to <see cref="TicketListItemDto"/>.
/// </summary>
public sealed class ListTicketsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to name each ticket's client through the Clients module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to name each ticket's client through the Clients module.</param>
    public ListTicketsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the requested page. This use case refuses nothing.</returns>
    public async Task<Result<PaginatedList<TicketListItemDto>>> HandleAsync(
        ListTicketsQuery query, CancellationToken ct)
    {
        var ticketsQuery = _context.Tickets.AsNoTracking().Matching(query);

        if (query.Flagged)
        {
            ticketsQuery = ticketsQuery.Where(t => t.IsFlagged);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var orderedTickets = ticketsQuery.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id);

        var totalCount = await orderedTickets.CountAsync(ct);

        // The list needs each ticket's reply count and last reply moment, not the replies: both are
        // computed in SQL, so a page never loads the conversations of its tickets.
        var tickets = await orderedTickets
            .Paginate(query.Page, query.PageSize)
            .Select(t => new
            {
                t.Id,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAt,
                ReplyCount = t.Replies.Count(),
                LastReplyAt = t.Replies.Max(r => (DateTimeOffset?)r.CreatedAt),
                t.DepartmentId,
                t.IsFlagged,
                t.ClientId,
            })
            .ToListAsync(ct);

        var departments = await _context.Departments.AsNoTracking().ToListAsync(ct);
        var deptMap = departments.ToDictionary(d => d.Id, d => d.Name);

        // FK_Tickets_Clients_ClientId restricts deleting a client that has tickets, so every owner is there.
        var clientIds = tickets.Select(t => t.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var items = tickets
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.ReplyCount,
                t.DepartmentId.HasValue && deptMap.TryGetValue(t.DepartmentId.Value, out var name) ? name : null,
                t.LastReplyAt,
                t.IsFlagged,
                t.ClientId,
                clients[t.ClientId].FullName))
            .ToList();

        return Result.Success(new PaginatedList<TicketListItemDto>(items, totalCount, query.Page, query.PageSize));
    }
}
