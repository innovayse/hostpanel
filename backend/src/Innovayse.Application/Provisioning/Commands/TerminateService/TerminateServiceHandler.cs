namespace Innovayse.Application.Provisioning.Commands.TerminateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Permanently terminates a hosting service by calling the provisioning provider
/// and marking the service aggregate as terminated.
/// </summary>
public sealed class TerminateServiceHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Handles <see cref="TerminateServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The terminate command containing the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task HandleAsync(TerminateServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        var request = new TerminateRequest(service.Id, service.ProvisioningRef, cmd.Reason);
        await provisioningProvider.TerminateAsync(request, ct);

        service.Terminate(cmd.Reason);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
