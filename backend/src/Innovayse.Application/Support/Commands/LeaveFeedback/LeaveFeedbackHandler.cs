namespace Innovayse.Application.Support.Commands.LeaveFeedback;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="LeaveFeedbackCommand"/>.</summary>
public sealed class LeaveFeedbackHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>Records feedback on the ticket.</summary>
    public async Task HandleAsync(LeaveFeedbackCommand command, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(command.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {command.TicketId} not found.");

        ticket.LeaveFeedback(command.Rating, command.Comment, command.LeftBy);
        await uow.SaveChangesAsync(ct);
    }
}
