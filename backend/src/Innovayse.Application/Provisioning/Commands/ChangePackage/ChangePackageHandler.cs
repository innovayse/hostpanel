namespace Innovayse.Application.Provisioning.Commands.ChangePackage;

using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Changes the hosting package of a provisioned account by calling the provisioning provider.
/// </summary>
public sealed class ChangePackageHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory)
{
    /// <summary>
    /// Handles <see cref="ChangePackageCommand"/>.
    /// </summary>
    /// <param name="cmd">The command containing the service identifier and new package name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, has no provisioning reference, or has no server assigned.
    /// </exception>
    public async Task HandleAsync(ChangePackageCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        var server = service.ServerId.HasValue
            ? await serverRepo.FindByIdAsync(service.ServerId.Value, ct)
            : null;

        if (server is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no server assigned.");
        }

        var provider = providerFactory.CreateFor(server);
        await provider.ChangePackageAsync(service.ProvisioningRef, cmd.NewPackage, ct);
    }
}
