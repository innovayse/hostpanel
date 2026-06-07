namespace Innovayse.Application.Provisioning.Commands.TerminateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Permanently terminates a hosting service by looking up the assigned server,
/// calling the provisioning provider, and marking the service as terminated.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="serverRepo">Server repository to look up the assigned server.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="unitOfWork">Unit of work for persistence.</param>
public sealed class TerminateServiceHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory,
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

        if (service.ServerId is not null)
        {
            var server = await serverRepo.FindByIdAsync(service.ServerId.Value, ct);
            if (server is not null)
            {
                var provider = providerFactory.CreateFor(server);
                var request = new TerminateRequest(service.Id, service.ProvisioningRef, cmd.Reason);
                await provider.TerminateAsync(request, ct);
            }
        }

        service.Terminate(cmd.Reason);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
