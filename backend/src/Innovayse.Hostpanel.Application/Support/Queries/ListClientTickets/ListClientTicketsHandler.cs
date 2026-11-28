using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListClientTickets;

/// <summary>
/// Returns a paginated list of tickets for a specific client with department names resolved, narrowed by the
/// search text on the subject and the ticket id and by status, priority and department, all in SQL ahead of paging.
/// </summary>
public sealed class ListClientTicketsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to name the tickets' client through the Clients module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to name the tickets' client through the Clients module.</param>
    public ListClientTicketsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client, the paging window, the optional search and the filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of ticket summaries. This use case answers no refusal code.</returns>
    public async Task<Result<PaginatedList<TicketListItemDto>>> HandleAsync(
        ListClientTicketsQuery query, CancellationToken ct)
    {
        // The id ends the order so a page never repeats or skips a row between requests.
        var tickets = _context.Tickets.AsNoTracking()
            .Where(t => t.ClientId == query.ClientId)
            .Matching(query)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);

        var totalCount = await tickets.CountAsync(ct);
        var items = await tickets
            .Paginate(query.Page, query.PageSize)
            .GroupJoin(
                _context.Departments.AsNoTracking(),
                t => t.DepartmentId,
                d => (int?)d.Id,
                (t, departments) => new { Ticket = t, Departments = departments })
            .SelectMany(
                x => x.Departments.DefaultIfEmpty(),
                (x, department) => new TicketListItemDto(
                    x.Ticket.Id,
                    x.Ticket.Subject,
                    x.Ticket.Status,
                    x.Ticket.Priority,
                    x.Ticket.CreatedAt,
                    x.Ticket.Replies.Count,
                    department != null ? department.Name : null,
                    x.Ticket.Replies.Count > 0 ? x.Ticket.Replies.Max(r => r.CreatedAt) : (DateTimeOffset?)null,
                    x.Ticket.IsFlagged,
                    x.Ticket.ClientId,
                    string.Empty))
            .ToListAsync(ct);

        // FK_Tickets_Clients_ClientId restricts deleting a client that has tickets, so a page that
        // holds a ticket names a client that exists; an empty page asks for nobody.
        var clientIds = items.Select(t => t.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);
        var named = items.Select(t => t with { ClientName = clients[t.ClientId].FullName }).ToList();

        return Result.Success(new PaginatedList<TicketListItemDto>(named, totalCount, query.Page, query.PageSize));
    }
}
