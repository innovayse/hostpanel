namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="ServiceSuspendedEvent"/> by suspending the hosting account
/// on the assigned server's provisioning provider. Delivered asynchronously.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="logger">Logger for provider failures that leave the server out of step with the platform.</param>
public sealed class ServiceSuspendedHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory,
    ILogger<ServiceSuspendedHandler> logger)
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
        catch (Exception ex)
        {
            // The benign case — the command handler already suspended the account on the server —
            // is not distinguishable here: the provider reports it the same way it reports access
            // denied, an unreachable host, or a module the factory has no provider for. So the
            // handler still continues, but it no longer does so silently. Without this line a
            // suspension that never reached the server leaves the platform showing "Suspended"
            // while the site stays up and billing stops, with nothing anywhere to say so.
            logger.LogWarning(
                ex,
                "Suspending service {ServiceId} on server {ServerId} failed; the platform shows it as suspended but the hosting account may still be live.",
                service.Id,
                server.Id);
        }
    }
}
