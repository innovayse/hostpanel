namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Handles <see cref="ServiceSuspendedEvent"/> by suspending the hosting account
/// on the provisioning provider (e.g. cPanel WHM). Delivered asynchronously via RabbitMQ.
/// </summary>
public sealed class ServiceSuspendedHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provider)
{
    /// <summary>
    /// Suspends the hosting account on the provider when a service is suspended.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceSuspendedEvent evt, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(evt.ServiceId, ct);
        if (service?.ProvisioningRef is null)
        {
            return;
        }

        var request = new SuspendRequest(service.Id, service.ProvisioningRef, evt.Reason);
        await provider.SuspendAsync(request, ct);
    }
}
