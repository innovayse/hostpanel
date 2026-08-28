namespace Innovayse.Application.Support.Queries.GetMyTickets;

using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns every ticket belonging to the calling client, mapped to <see cref="TicketListItemDto"/>.
/// </summary>
/// <param name="repo">Ticket repository.</param>
/// <param name="clientRepo">Resolves the caller's client record.</param>
/// <param name="caller">Who is asking; the query does not say, and must not.</param>
public sealed class GetMyTicketsHandler(
    ITicketRepository repo,
    IClientRepository clientRepo,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="GetMyTicketsQuery"/>.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of the client's ticket summary DTOs.</returns>
    /// <exception cref="ClientProfileNotFoundException">
    /// Thrown when the caller has no client record. Answering an empty list instead would
    /// make "you have no account here" indistinguishable from "you have no tickets".
    /// </exception>
    public async Task<IReadOnlyList<TicketListItemDto>> HandleAsync(GetMyTicketsQuery query, CancellationToken ct)
    {
        var userId = caller.RequireUserId();
        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        var tickets = await repo.ListByClientIdAsync(client.Id, ct);
        return tickets
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Subject,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.CreatedAt,
                t.Replies.Count,
                null,
                t.Replies.Count > 0 ? t.Replies.Max(r => r.CreatedAt) : null,
                t.IsFlagged,
                t.ClientId))
            .ToList();
    }
}
