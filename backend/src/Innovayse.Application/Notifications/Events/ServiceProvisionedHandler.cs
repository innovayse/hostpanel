namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Provisioning.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="ServiceProvisionedEvent"/> and dispatches a service-ready notification email.
/// </summary>
/// <remarks>
/// The handler does not have the client email directly from the event.
/// TODO: Resolve client email via IClientRepository if email delivery is required.
/// </remarks>
public sealed class ServiceProvisionedHandler(IMessageBus bus)
{
    /// <summary>
    /// Placeholder — currently a no-op until client email lookup is wired up.
    /// </summary>
    /// <param name="evt">The service provisioned domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task HandleAsync(ServiceProvisionedEvent evt, CancellationToken ct)
    {
        // TODO: Resolve client email from IClientRepository, then dispatch:
        // var data = new { service = new { id = evt.ServiceId, ref = evt.ProvisioningRef } };
        // await bus.InvokeAsync(new SendEmailCommand(clientEmail, "service-provisioned", data), ct);
        _ = bus;
        _ = evt;
        _ = ct;
        return Task.CompletedTask;
    }
}
