namespace Innovayse.Application.Support.Commands.DeleteTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="DeleteTicketCommand"/> by permanently removing the ticket.</summary>
public sealed class DeleteTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes a ticket regardless of its current status.
    /// </summary>
    /// <param name="cmd">The delete ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found.</exception>
    public async Task HandleAsync(DeleteTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(cmd.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {cmd.TicketId} not found.");

        repo.Remove(ticket);
        await uow.SaveChangesAsync(ct);
    }
}
