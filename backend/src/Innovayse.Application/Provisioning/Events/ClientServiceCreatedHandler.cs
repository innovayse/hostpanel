namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Domain.Services.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="ClientServiceCreatedEvent"/> to trigger automatic provisioning
/// of the newly ordered hosting service.
/// </summary>
public sealed class ClientServiceCreatedHandler(IMessageBus bus)
{
    /// <summary>
    /// Dispatches a <see cref="ProvisionServiceCommand"/> when a service is created.
    /// </summary>
    /// <param name="evt">The domain event carrying the new service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ClientServiceCreatedEvent evt, CancellationToken ct)
        => await bus.InvokeAsync(new ProvisionServiceCommand(evt.ServiceId), ct);
}
