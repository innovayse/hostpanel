namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="ServiceTerminatedEvent"/> by terminating the hosting account
/// on the assigned server's provisioning provider. Delivered asynchronously.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="logger">Logger for provider failures that leave the server out of step with the platform.</param>
public sealed class ServiceTerminatedHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory,
    ILogger<ServiceTerminatedHandler> logger)
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

        try
        {
            var provider = providerFactory.CreateFor(server);
            var request = new TerminateRequest(service.Id, service.ProvisioningRef, evt.Reason);
            await provider.TerminateAsync(request, ct);
        }
        catch (Exception ex)
        {
            // The benign case — the command handler already terminated the account — is not
            // distinguishable here from access denied, an unreachable host, or a module the
            // factory has no provider for. The handler continues, but no longer in silence: a
            // termination that never reached the server leaves a live, unbilled hosting account.
            logger.LogWarning(
                ex,
                "Terminating service {ServiceId} on server {ServerId} failed; the platform shows it as terminated but the hosting account may still exist.",
                service.Id,
                server.Id);
        }
    }
}
