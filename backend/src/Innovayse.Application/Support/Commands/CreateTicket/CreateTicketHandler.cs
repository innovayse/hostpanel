namespace Innovayse.Application.Support.Commands.CreateTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new support ticket and persists it via <see cref="ITicketRepository"/>.
/// </summary>
public sealed class CreateTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateTicketCommand"/>.
    /// </summary>
    /// <param name="cmd">The create ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created ticket ID.</returns>
    /// <exception cref="ArgumentException">Propagated from domain when subject or message is null or whitespace.</exception>
    public async Task<int> HandleAsync(CreateTicketCommand cmd, CancellationToken ct)
    {
        var priority = Enum.Parse<TicketPriority>(cmd.Priority, ignoreCase: true);
        var ticket = Ticket.Create(cmd.ClientId, cmd.Subject, cmd.Message, cmd.DepartmentId, priority);

        repo.Add(ticket);
        await uow.SaveChangesAsync(ct);
        return ticket.Id;
    }
}
