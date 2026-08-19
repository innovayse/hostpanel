namespace Innovayse.Application.Support.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support.Events;
using Innovayse.Domain.Support.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="TicketClosedEvent"/> raised when a support ticket is closed.
/// Notifies the client so they know the ticket is resolved and can leave feedback.
/// </summary>
public sealed class TicketClosedHandler(
    IMessageBus bus,
    ITicketRepository ticketRepo,
    IClientRepository clientRepo,
    IIdentityProvider identity)
{
    /// <summary>
    /// Resolves the client's email and sends a ticket-closed notification email.
    /// </summary>
    /// <param name="evt">The ticket closed domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(TicketClosedEvent evt, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
        if (client is null) return;

        var user = await identity.FindBySubjectAsync(client.UserId, ct);
        if (user is null) return;

        var ticket = await ticketRepo.FindByIdAsync(evt.TicketId, ct);
        var subject = ticket?.Subject ?? string.Empty;

        var data = new { ticket = new { id = evt.TicketId, subject } };

        await bus.InvokeAsync(new SendEmailCommand(user.Email, "ticket-closed", data), ct);
    }
}
