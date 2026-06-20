namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Clients.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="InvoiceCreatedEvent"/> and dispatches an invoice-created notification email.
/// </summary>
public sealed class InvoiceCreatedHandler(
    IMessageBus bus,
    IClientRepository clientRepo,
    IUserService userService)
{
    /// <summary>
    /// Resolves the client email and sends an invoice-created notification email.
    /// </summary>
    /// <param name="evt">The invoice created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(InvoiceCreatedEvent evt, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
        if (client is null)
        {
            return;
        }

        var user = await userService.FindByIdAsync(client.UserId, ct);
        if (user is null)
        {
            return;
        }

        var data = new
        {
            invoice = new { id = evt.InvoiceId }
        };

        await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "invoice-created", data), ct);
    }
}
