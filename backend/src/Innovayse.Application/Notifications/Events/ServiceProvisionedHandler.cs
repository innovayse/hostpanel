namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Services.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="ServiceProvisionedEvent"/> and dispatches a service-ready notification email.
/// </summary>
public sealed class ServiceProvisionedHandler(
    IMessageBus bus,
    IClientServiceRepository serviceRepo,
    IClientRepository clientRepo,
    IUserService userService)
{
    /// <summary>
    /// Resolves the client email and sends a service-provisioned notification email.
    /// </summary>
    /// <param name="evt">The service provisioned domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(ServiceProvisionedEvent evt, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(evt.ServiceId, ct);
        if (service is null) return;

        var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
        if (client is null) return;

        var user = await userService.FindByIdAsync(client.UserId, ct);
        if (user is null) return;

        var data = new
        {
            service = new
            {
                id = evt.ServiceId,
                provisioningRef = evt.ProvisioningRef,
                domain = service.Domain,
                username = service.Username
            }
        };

        await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "service-provisioned", data), ct);
    }
}
