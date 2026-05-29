namespace Innovayse.Application.Support.Commands.ToggleTicketFlag;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Toggles the flagged state of a support ticket.
/// </summary>
public sealed class ToggleTicketFlagHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="ToggleTicketFlagCommand"/>.
    /// </summary>
    /// <param name="command">The toggle flag command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found.</exception>
    public async Task HandleAsync(ToggleTicketFlagCommand command, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(command.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {command.TicketId} not found.");

        ticket.ToggleFlag();
        await uow.SaveChangesAsync(ct);
    }
}
