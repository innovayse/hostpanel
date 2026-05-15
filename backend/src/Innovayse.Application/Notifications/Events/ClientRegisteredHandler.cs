namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="ClientCreatedEvent"/> and dispatches a welcome email to the new client.
/// </summary>
public sealed class ClientRegisteredHandler(IMessageBus bus)
{
    /// <summary>
    /// Sends a welcome email when a new client registers.
    /// </summary>
    /// <param name="evt">The client created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(ClientCreatedEvent evt, CancellationToken ct)
    {
        var data = new { client = new { email = evt.Email } };
        await bus.InvokeAsync(new SendEmailCommand(evt.Email, "welcome", data), ct);
    }
}
