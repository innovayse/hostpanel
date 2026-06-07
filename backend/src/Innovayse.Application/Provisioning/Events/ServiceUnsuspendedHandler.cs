namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="ServiceUnsuspendedEvent"/> by unsuspending the hosting account
/// on the assigned server's provisioning provider. Delivered asynchronously.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
public sealed class ServiceUnsuspendedHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory)
{
    /// <summary>
    /// Unsuspends the hosting account on the provider when a service is restored.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceUnsuspendedEvent evt, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(evt.ServiceId, ct);
        if (service?.ProvisioningRef is null || service.ServerId is null)
        {
            return;
        }

        var server = await serverRepo.FindByIdAsync(service.ServerId.Value, ct);
        if (server is null)
        {
            return;
        }

        var provider = providerFactory.CreateFor(server);
        await provider.UnsuspendAsync(service.ProvisioningRef, ct);
    }
}
