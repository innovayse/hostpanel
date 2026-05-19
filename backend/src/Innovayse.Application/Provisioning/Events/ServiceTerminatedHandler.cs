namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Handles <see cref="ServiceTerminatedEvent"/> by terminating the hosting account
/// on the provisioning provider (e.g. cPanel WHM). Delivered asynchronously via RabbitMQ.
/// </summary>
public sealed class ServiceTerminatedHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provider)
{
    /// <summary>
    /// Terminates the hosting account on the provider when a service is permanently terminated.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceTerminatedEvent evt, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(evt.ServiceId, ct);
        if (service?.ProvisioningRef is null)
        {
            return;
        }

        var request = new TerminateRequest(service.Id, service.ProvisioningRef, evt.Reason);
        await provider.TerminateAsync(request, ct);
    }
}
