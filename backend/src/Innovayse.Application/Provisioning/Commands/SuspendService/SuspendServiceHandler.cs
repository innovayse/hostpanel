namespace Innovayse.Application.Provisioning.Commands.SuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Suspends an active hosting service by calling the provisioning provider
/// and updating the service aggregate status.
/// </summary>
public sealed class SuspendServiceHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Handles <see cref="SuspendServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The suspend command containing the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task HandleAsync(SuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        var request = new SuspendRequest(service.Id, service.ProvisioningRef, cmd.Reason);
        await provisioningProvider.SuspendAsync(request, ct);

        service.Suspend(cmd.Reason);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
