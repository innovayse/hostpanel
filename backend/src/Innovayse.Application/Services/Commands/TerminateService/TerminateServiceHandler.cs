namespace Innovayse.Application.Services.Commands.TerminateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Permanently terminates a client service.</summary>
public sealed class TerminateServiceHandler(
    IClientServiceRepository repo,
    IProvisioningProvider provisioner,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="TerminateServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The terminate command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task HandleAsync(TerminateServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is not null)
        {
            await provisioner.TerminateAsync(service.ProvisioningRef, ct);
        }

        service.Terminate();
        await uow.SaveChangesAsync(ct);
    }
}
