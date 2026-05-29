namespace Innovayse.Application.Support.Commands.UpdateTicket;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="UpdateTicketCommand"/> by applying metadata changes to a ticket.</summary>
public sealed class UpdateTicketHandler(ITicketRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates a ticket's metadata fields.
    /// </summary>
    /// <param name="cmd">The update ticket command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found.</exception>
    public async Task HandleAsync(UpdateTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(cmd.TicketId, ct)
            ?? throw new InvalidOperationException($"Ticket {cmd.TicketId} not found.");

        if (cmd.Priority is not null)
        {
            var priority = Enum.Parse<TicketPriority>(cmd.Priority, true);
            ticket.ChangePriority(priority);
        }

        if (cmd.DepartmentId is not null)
        {
            ticket.ChangeDepartment(cmd.DepartmentId);
        }

        if (cmd.AssignedToStaffId is not null)
        {
            ticket.Assign(cmd.AssignedToStaffId.Value);
        }

        if (cmd.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(cmd.Status, true);
            if (status == TicketStatus.Closed && ticket.Status != TicketStatus.Closed)
            {
                ticket.Close();
            }
            else if (status == TicketStatus.Open && ticket.Status == TicketStatus.Closed)
            {
                ticket.Reopen();
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
