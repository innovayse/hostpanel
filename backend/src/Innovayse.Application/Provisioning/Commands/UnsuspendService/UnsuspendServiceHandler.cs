namespace Innovayse.Application.Provisioning.Commands.UnsuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Re-activates a suspended hosting service by calling the provisioning provider
/// and restoring the service aggregate to active status.
/// </summary>
public sealed class UnsuspendServiceHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Handles <see cref="UnsuspendServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The unsuspend command containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task HandleAsync(UnsuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        await provisioningProvider.UnsuspendAsync(service.ProvisioningRef, ct);

        service.Unsuspend();

        await unitOfWork.SaveChangesAsync(ct);
    }
}
