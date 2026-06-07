namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="ServiceTerminatedEvent"/> by terminating the hosting account
/// on the assigned server's provisioning provider. Delivered asynchronously.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
public sealed class ServiceTerminatedHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory)
{
    /// <summary>
    /// Terminates the hosting account on the provider when a service is permanently terminated.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceTerminatedEvent evt, CancellationToken ct)
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
        var request = new TerminateRequest(service.Id, service.ProvisioningRef, evt.Reason);
        await provider.TerminateAsync(request, ct);
    }
}
