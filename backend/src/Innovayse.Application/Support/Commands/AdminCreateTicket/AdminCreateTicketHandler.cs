namespace Innovayse.Application.Support.Commands.AdminCreateTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="AdminCreateTicketCommand"/> by creating a new ticket.</summary>
public sealed class AdminCreateTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new support ticket on behalf of a client.
    /// </summary>
    /// <param name="cmd">The admin create ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created ticket.</returns>
    public async Task<int> HandleAsync(AdminCreateTicketCommand cmd, CancellationToken ct)
    {
        var priority = Enum.Parse<TicketPriority>(cmd.Priority, true);
        var ticket = Ticket.Create(cmd.ClientId, cmd.Subject, cmd.Message, cmd.DepartmentId, priority);

        repo.Add(ticket);
        await uow.SaveChangesAsync(ct);

        return ticket.Id;
    }
}
