namespace Innovayse.Application.Support.Commands.ReplyToTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Posts a reply on an existing ticket and persists the updated aggregate.
/// </summary>
public sealed class ReplyToTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="ReplyToTicketCommand"/>.
    /// </summary>
    /// <param name="cmd">The reply command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found or is already closed.</exception>
    public async Task HandleAsync(ReplyToTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(cmd.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket with ID {cmd.TicketId} was not found.");

        ticket.AddReply(cmd.Message, cmd.AuthorName, cmd.IsStaffReply);
        await uow.SaveChangesAsync(ct);
    }
}
