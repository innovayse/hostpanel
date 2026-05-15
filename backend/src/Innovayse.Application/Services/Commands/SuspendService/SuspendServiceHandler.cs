namespace Innovayse.Application.Services.Commands.SuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Suspends an active client service.</summary>
public sealed class SuspendServiceHandler(
    IClientServiceRepository repo,
    IProvisioningProvider provisioner,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SuspendServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The suspend command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task HandleAsync(SuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is not null)
        {
            await provisioner.SuspendAsync(service.ProvisioningRef, ct);
        }

        service.Suspend();
        await uow.SaveChangesAsync(ct);
    }
}
