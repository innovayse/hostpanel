namespace Innovayse.Application.Services.Commands.TerminateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Permanently terminates a client service. Provisioning is handled asynchronously via domain events.</summary>
public sealed class TerminateServiceHandler(
    IClientServiceRepository repo,
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

        service.Terminate();
        await uow.SaveChangesAsync(ct);
    }
}
