namespace Innovayse.Application.Support.Commands.AddTicketTag;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="AddTicketTagCommand"/>.</summary>
public sealed class AddTicketTagHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>Adds a tag to the ticket.</summary>
    public async Task HandleAsync(AddTicketTagCommand command, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(command.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {command.TicketId} not found.");

        ticket.AddTag(command.Tag);
        await uow.SaveChangesAsync(ct);
    }
}
