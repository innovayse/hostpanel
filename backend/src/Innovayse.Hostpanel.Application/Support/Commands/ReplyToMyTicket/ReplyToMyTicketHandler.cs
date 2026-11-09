using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Commands.ReplyToTicket;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Support.Commands.ReplyToMyTicket;

/// <summary>
/// Posts a client's reply on one of their own tickets, refusing every ticket that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// write a reply through <see cref="ReplyToMyTicketCommand"/> without it having run. Once ownership
/// is settled the write is the same one the admin route performs, so this dispatches
/// <see cref="ReplyToTicketCommand"/> with the staff flag fixed to <see langword="false"/> rather
/// than duplicating the aggregate call.
/// </remarks>
public sealed class ReplyToMyTicketHandler
{
    /// <summary>The rule that says a client may only reply to their own tickets.</summary>
    private readonly ITicketOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared write once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only reply to their own tickets.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared write once ownership is settled.</param>
    public ReplyToMyTicketHandler(ITicketOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ReplyToMyTicketCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this replies on the caller's own ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the reply has been persisted, or <c>TICKET_NOT_FOUND</c> when the ticket is not
    /// the caller's, when no such ticket exists, or when the caller has no client record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(ReplyToMyTicketCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.TicketId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(
            new ReplyToTicketCommand(cmd.TicketId, cmd.Message, IsStaffReply: false),
            ct);
    }
}
