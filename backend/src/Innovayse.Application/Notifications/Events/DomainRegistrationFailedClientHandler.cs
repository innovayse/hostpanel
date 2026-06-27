namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainRegistrationFailedEvent"/> and notifies the affected client
/// by email that their domain registration failed and a refund has been issued.
/// </summary>
public sealed class DomainRegistrationFailedClientHandler(
    IMessageBus bus,
    IClientRepository clientRepo,
    IUserService userService)
{
    /// <summary>
    /// Resolves the client email and dispatches a domain-registration-failed notification email.
    /// </summary>
    /// <param name="evt">The domain registration failed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainRegistrationFailedEvent evt, CancellationToken ct)
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
            domain = new { name = evt.DomainName },
            invoice = new { id = evt.InvoiceId },
        };

        await bus.InvokeAsync(
            new SendEmailCommand(user.Value.Email, "domain-registration-failed", data), ct);
    }
}
