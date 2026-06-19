namespace Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;

using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Generates a time-limited cPanel SSO URL for direct client access to their hosting account.
/// </summary>
public sealed class GetCPanelSsoUrlHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory)
{
    /// <summary>
    /// Handles <see cref="GetCPanelSsoUrlQuery"/>.
    /// </summary>
    /// <param name="query">The query containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited cPanel SSO URL string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, has no provisioning reference, or has no server assigned.
    /// </exception>
    public async Task<string> HandleAsync(GetCPanelSsoUrlQuery query, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(query.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {query.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {query.ServiceId} has not been provisioned yet.");
        }

        var server = service.ServerId.HasValue
            ? await serverRepo.FindByIdAsync(service.ServerId.Value, ct)
            : null;

        if (server is null)
        {
            throw new InvalidOperationException($"ClientService {query.ServiceId} has no server assigned.");
        }

        var provider = providerFactory.CreateFor(server);
        return await provider.GetCPanelSsoUrlAsync(service.ProvisioningRef, ct);
    }
}
