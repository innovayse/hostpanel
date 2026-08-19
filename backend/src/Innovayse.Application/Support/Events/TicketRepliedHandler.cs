namespace Innovayse.Application.Support.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support.Events;
using Innovayse.Domain.Support.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="TicketRepliedEvent"/> raised when a reply is added to a ticket.
/// A staff reply notifies the client; a client reply notifies the assigned department.
/// </summary>
public sealed class TicketRepliedHandler(
    IMessageBus bus,
    ITicketRepository ticketRepo,
    IDepartmentRepository departmentRepo,
    IClientRepository clientRepo,
    IIdentityProvider identity)
{
    /// <summary>
    /// Resolves the recipient (client or department, depending on the reply's author) and
    /// sends a ticket-replied notification email.
    /// </summary>
    /// <param name="evt">The ticket replied domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(TicketRepliedEvent evt, CancellationToken ct)
    {
        var ticket = await ticketRepo.FindByIdAsync(evt.TicketId, ct);
        if (ticket is null)
        {
            return;
        }

        var data = new { ticket = new { id = evt.TicketId, subject = ticket.Subject } };

        if (evt.IsStaffReply)
        {
            var client = await clientRepo.FindByIdAsync(ticket.ClientId, ct);
            if (client is null) return;

            var user = await identity.FindBySubjectAsync(client.UserId, ct);
            if (user is null) return;

            await bus.InvokeAsync(new SendEmailCommand(user.Email, "ticket-replied", data), ct);
        }
        else if (ticket.DepartmentId is int departmentId)
        {
            var department = await departmentRepo.FindByIdAsync(departmentId, ct);
            if (department is null || string.IsNullOrWhiteSpace(department.Email)) return;

            await bus.InvokeAsync(new SendEmailCommand(department.Email, "ticket-replied", data), ct);
        }
    }
}
