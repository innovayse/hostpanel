namespace Innovayse.Application.Support.Commands.ReplyToMyTicket;

using Innovayse.Application.Support.Commands.ReplyToTicket;
using Innovayse.Application.Support.Common;
using Wolverine;

/// <summary>
/// Posts a client's reply on one of their own tickets, refusing every ticket that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// write a reply through <see cref="ReplyToMyTicketCommand"/> without it having run. Once
/// ownership is settled the write is the same one the admin route performs, so this dispatches
/// <see cref="ReplyToTicketCommand"/> with the staff flag fixed to <see langword="false"/>
/// rather than duplicating the aggregate call.
/// </remarks>
/// <param name="ownership">The rule that says a client may only reply to their own tickets.</param>
/// <param name="bus">Wolverine bus, used to reach the shared write once ownership is settled.</param>
public sealed class ReplyToMyTicketHandler(ITicketOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="ReplyToMyTicketCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this replies on the caller's own ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the reply has been persisted.</returns>
    /// <exception cref="TicketNotFoundException">
    /// Thrown when the ticket is not the caller's, when no such ticket exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(ReplyToMyTicketCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.TicketId, ct);

        await bus.InvokeAsync(
            new ReplyToTicketCommand(cmd.TicketId, cmd.Message, cmd.AuthorName, IsStaffReply: false),
            ct);
    }
}
