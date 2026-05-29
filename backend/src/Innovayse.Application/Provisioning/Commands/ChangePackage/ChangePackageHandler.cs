namespace Innovayse.Application.Provisioning.Commands.ChangePackage;

using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Changes the hosting package of a provisioned account by calling the provisioning provider.
/// </summary>
public sealed class ChangePackageHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider)
{
    /// <summary>
    /// Handles <see cref="ChangePackageCommand"/>.
    /// </summary>
    /// <param name="cmd">The command containing the service identifier and new package name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task HandleAsync(ChangePackageCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        await provisioningProvider.ChangePackageAsync(service.ProvisioningRef, cmd.NewPackage, ct);
    }
}
