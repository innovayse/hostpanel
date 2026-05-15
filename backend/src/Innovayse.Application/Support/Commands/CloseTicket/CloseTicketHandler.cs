namespace Innovayse.Application.Support.Commands.CloseTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Closes an existing support ticket and persists the status transition.
/// </summary>
public sealed class CloseTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CloseTicketCommand"/>.
    /// </summary>
    /// <param name="cmd">The close ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found or is already closed.</exception>
    public async Task HandleAsync(CloseTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(cmd.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket with ID {cmd.TicketId} was not found.");

        ticket.Close();
        await uow.SaveChangesAsync(ct);
    }
}
