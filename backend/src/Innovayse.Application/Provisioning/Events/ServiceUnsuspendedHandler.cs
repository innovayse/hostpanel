namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Handles <see cref="ServiceUnsuspendedEvent"/> by unsuspending the hosting account
/// on the provisioning provider (e.g. cPanel WHM). Delivered asynchronously via RabbitMQ.
/// </summary>
public sealed class ServiceUnsuspendedHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provider)
{
    /// <summary>
    /// Unsuspends the hosting account on the provider when a service is restored.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceUnsuspendedEvent evt, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(evt.ServiceId, ct);
        if (service?.ProvisioningRef is null)
        {
            return;
        }

        await provider.UnsuspendAsync(service.ProvisioningRef, ct);
    }
}
