namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="ServiceSuspendedEvent"/> by suspending the hosting account
/// on the assigned server's provisioning provider. Delivered asynchronously.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
public sealed class ServiceSuspendedHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory)
{
    /// <summary>
    /// Suspends the hosting account on the provider when a service is suspended.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceSuspendedEvent evt, CancellationToken ct)
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

        try
        {
            var provider = providerFactory.CreateFor(server);
            var request = new SuspendRequest(service.Id, service.ProvisioningRef, evt.Reason);
            await provider.SuspendAsync(request, ct);
        }
        catch
        {
            // Account may already be suspended on the server by the command handler — safe to ignore
        }
    }
}
