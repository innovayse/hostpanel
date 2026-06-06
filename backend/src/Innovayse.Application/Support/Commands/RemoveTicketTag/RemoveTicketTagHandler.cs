namespace Innovayse.Application.Support.Commands.RemoveTicketTag;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="RemoveTicketTagCommand"/>.</summary>
public sealed class RemoveTicketTagHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>Removes a tag from the ticket.</summary>
    public async Task HandleAsync(RemoveTicketTagCommand command, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(command.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {command.TicketId} not found.");

        ticket.RemoveTag(command.Tag);
        await uow.SaveChangesAsync(ct);
    }
}
