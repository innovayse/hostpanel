namespace Innovayse.Application.Provisioning.Queries.GetServiceCredentials;

using Innovayse.Application.Provisioning.DTOs;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Retrieves the hosting credentials for a provisioned service by delegating
/// to the configured provisioning provider.
/// </summary>
public sealed class GetServiceCredentialsHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider)
{
    /// <summary>
    /// Handles <see cref="GetServiceCredentialsQuery"/>.
    /// </summary>
    /// <param name="query">The query containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ServiceCredentialsDto"/> with the current credentials.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task<ServiceCredentialsDto> HandleAsync(GetServiceCredentialsQuery query, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(query.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {query.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {query.ServiceId} has not been provisioned yet.");
        }

        var credentials = await provisioningProvider.GetCredentialsAsync(service.ProvisioningRef, ct);

        return new ServiceCredentialsDto(
            credentials.Username,
            credentials.Password,
            credentials.Domain,
            credentials.ServerIp,
            credentials.CpanelUrl);
    }
}
