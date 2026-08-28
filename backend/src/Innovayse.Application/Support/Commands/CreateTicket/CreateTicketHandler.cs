namespace Innovayse.Application.Support.Commands.CreateTicket;

using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new support ticket on the calling client's own account and persists it via
/// <see cref="ITicketRepository"/>.
/// </summary>
/// <param name="repo">Ticket repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="clientRepo">Resolves the caller's client record.</param>
/// <param name="caller">Who is filing it; the command does not say, and must not.</param>
public sealed class CreateTicketHandler(
    ITicketRepository repo,
    IUnitOfWork uow,
    IClientRepository clientRepo,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="CreateTicketCommand"/>.
    /// </summary>
    /// <param name="cmd">The command. It names no account: the ticket is filed on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created ticket ID.</returns>
    /// <exception cref="ArgumentException">Propagated from domain when subject or message is null or whitespace.</exception>
    /// <exception cref="ClientProfileNotFoundException">Thrown when the caller has no client record.</exception>
    public async Task<int> HandleAsync(CreateTicketCommand cmd, CancellationToken ct)
    {
        var userId = caller.RequireUserId();
        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        var priority = Enum.Parse<TicketPriority>(cmd.Priority, ignoreCase: true);
        var ticket = Ticket.Create(client.Id, cmd.Subject, cmd.Message, cmd.DepartmentId, priority);

        repo.Add(ticket);
        await uow.SaveChangesAsync(ct);
        return ticket.Id;
    }
}
