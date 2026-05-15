namespace Innovayse.Application.Support.Queries.ListTickets;

using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paged list of all support tickets mapped to <see cref="TicketListItemDto"/>.
/// </summary>
public sealed class ListTicketsHandler(ITicketRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListTicketsQuery"/>.
    /// </summary>
    /// <param name="query">The list tickets query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing ticket summary DTOs.</returns>
    public async Task<PagedResult<TicketListItemDto>> HandleAsync(ListTicketsQuery query, CancellationToken ct)
    {
        var tickets = await repo.ListAsync(query.Page, query.PageSize, ct);
        var items = tickets
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Subject,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.CreatedAt,
                t.Replies.Count))
            .ToList();

        // TotalCount is approximated; the domain interface does not expose a total-count method for all tickets.
        // A more precise implementation would require an additional repository method.
        var estimatedTotal = items.Count < query.PageSize
            ? (query.Page - 1) * query.PageSize + items.Count
            : query.Page * query.PageSize + 1;

        return new PagedResult<TicketListItemDto>(items, estimatedTotal, query.Page, query.PageSize);
    }
}
