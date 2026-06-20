namespace Innovayse.Application.Support.Commands.BulkUpdateTickets;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Performs a bulk action (close, delete, flag, unflag) on multiple tickets.
/// </summary>
public sealed class BulkUpdateTicketsHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="BulkUpdateTicketsCommand"/>.
    /// </summary>
    /// <param name="command">The bulk update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when the action is not recognised.</exception>
    public async Task HandleAsync(BulkUpdateTicketsCommand command, CancellationToken ct)
    {
        var action = command.Action.ToLowerInvariant();

        foreach (var ticketId in command.TicketIds)
        {
            var ticket = await repo.FindByIdAsync(ticketId, ct);
            if (ticket is null)
            {
                continue;
            }

            switch (action)
            {
                case "close":
                    if (ticket.Status != Domain.Support.TicketStatus.Closed)
                    {
                        ticket.Close();
                    }

                    break;
                case "delete":
                    repo.Remove(ticket);
                    break;
                case "flag":
                    if (!ticket.IsFlagged)
                    {
                        ticket.ToggleFlag();
                    }

                    break;
                case "unflag":
                    if (ticket.IsFlagged)
                    {
                        ticket.ToggleFlag();
                    }

                    break;
                default:
                    throw new ArgumentException($"Unknown bulk action: {command.Action}");
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
