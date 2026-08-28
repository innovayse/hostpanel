namespace Innovayse.Application.Support.Queries.GetMyTicket;

using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetTicket;
using Wolverine;

/// <summary>
/// Returns one of the calling client's own tickets, refusing every id that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// read a ticket through <see cref="GetMyTicketQuery"/> without it having run. Once ownership is
/// settled the projection is the same read the admin route performs, so this dispatches
/// <see cref="GetTicketQuery"/> rather than growing a second copy of the mapping that could
/// drift from it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only read their own tickets.</param>
/// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
public sealed class GetMyTicketHandler(ITicketOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyTicketQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="TicketDto"/>.</returns>
    /// <exception cref="TicketNotFoundException">
    /// Thrown when the ticket is not the caller's, when no such ticket exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<TicketDto> HandleAsync(GetMyTicketQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.Id, ct);
        return await bus.InvokeAsync<TicketDto>(new GetTicketQuery(query.Id), ct);
    }
}
