namespace Innovayse.Application.Support.Queries.GetMyTickets;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all tickets for a specific client mapped to <see cref="TicketListItemDto"/>.
/// </summary>
public sealed class GetMyTicketsHandler(ITicketRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetMyTicketsQuery"/>.
    /// </summary>
    /// <param name="query">The get my tickets query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of the client's ticket summary DTOs.</returns>
    public async Task<IReadOnlyList<TicketListItemDto>> HandleAsync(GetMyTicketsQuery query, CancellationToken ct)
    {
        var tickets = await repo.ListByClientIdAsync(query.ClientId, ct);
        return tickets
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Subject,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.CreatedAt,
                t.Replies.Count))
            .ToList();
    }
}
