namespace Innovayse.Application.Support.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Support.Events;
using Innovayse.Domain.Support.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="TicketCreatedEvent"/> raised when a new support ticket is opened.
/// Notifies the department the ticket was routed to, if the department has an email set.
/// </summary>
public sealed class TicketCreatedHandler(
    IMessageBus bus,
    ITicketRepository ticketRepo,
    IDepartmentRepository departmentRepo)
{
    /// <summary>
    /// Resolves the ticket and its department, then sends a ticket-created notification email
    /// to the department if one is configured.
    /// </summary>
    /// <param name="evt">The ticket created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(TicketCreatedEvent evt, CancellationToken ct)
    {
        var department = await departmentRepo.FindByIdAsync(evt.DepartmentId, ct);
        if (department is null || string.IsNullOrWhiteSpace(department.Email))
        {
            return;
        }

        var ticket = await ticketRepo.FindByIdAsync(evt.TicketId, ct);
        if (ticket is null)
        {
            return;
        }

        var data = new { ticket = new { id = evt.TicketId, subject = ticket.Subject } };

        await bus.InvokeAsync(new SendEmailCommand(department.Email, "ticket-created", data), ct);
    }
}
