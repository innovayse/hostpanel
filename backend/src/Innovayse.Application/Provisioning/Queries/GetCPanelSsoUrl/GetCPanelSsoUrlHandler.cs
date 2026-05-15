namespace Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;

using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Generates a time-limited cPanel SSO URL for direct client access to their hosting account.
/// </summary>
public sealed class GetCPanelSsoUrlHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider)
{
    /// <summary>
    /// Handles <see cref="GetCPanelSsoUrlQuery"/>.
    /// </summary>
    /// <param name="query">The query containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited cPanel SSO URL string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task<string> HandleAsync(GetCPanelSsoUrlQuery query, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(query.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {query.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {query.ServiceId} has not been provisioned yet.");
        }

        return await provisioningProvider.GetCPanelSsoUrlAsync(service.ProvisioningRef, ct);
    }
}
