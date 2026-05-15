namespace Innovayse.Application.Support.Commands.AssignTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Assigns an existing ticket to a staff member and persists the change.
/// </summary>
public sealed class AssignTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="AssignTicketCommand"/>.
    /// </summary>
    /// <param name="cmd">The assign ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found.</exception>
    public async Task HandleAsync(AssignTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(cmd.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket with ID {cmd.TicketId} was not found.");

        ticket.Assign(cmd.StaffId);
        await uow.SaveChangesAsync(ct);
    }
}
