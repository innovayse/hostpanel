namespace Innovayse.Application.Support.Common;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Resolves ticket ownership against the client the current credential names.
/// </summary>
/// <param name="tickets">Ticket repository.</param>
/// <param name="clients">Client repository, for mapping the caller's subject to their account.</param>
/// <param name="caller">Who is asking. Nothing tells this type whose tickets to consider.</param>
public sealed class TicketOwnership(
    ITicketRepository tickets,
    IClientRepository clients,
    ICurrentRequestContext caller) : ITicketOwnership
{
    /// <inheritdoc/>
    public async Task RequireOwnedByCallerAsync(int ticketId, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct);
        if (client is not null)
        {
            var ticket = await tickets.FindByIdAsync(ticketId, ct);
            if (ticket is not null && ticket.ClientId == client.Id)
            {
                return;
            }
        }

        // A ticket that does not exist, a ticket belonging to somebody else, and a caller with no
        // client record all land here and answer identically. Distinguishing them would turn this
        // route into a way of enumerating ids -- and ticket ids are sequential integers.
        throw new TicketNotFoundException(ticketId);
    }
}
