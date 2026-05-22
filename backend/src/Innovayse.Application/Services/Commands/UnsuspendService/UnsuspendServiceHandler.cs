namespace Innovayse.Application.Services.Commands.UnsuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Re-activates a previously suspended client service. Provisioning is handled asynchronously via domain events.</summary>
public sealed class UnsuspendServiceHandler(
    IClientServiceRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UnsuspendServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The unsuspend command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task HandleAsync(UnsuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        service.Unsuspend();
        await uow.SaveChangesAsync(ct);
    }
}
