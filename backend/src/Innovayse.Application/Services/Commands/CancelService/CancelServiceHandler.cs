namespace Innovayse.Application.Services.Commands.CancelService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Creates a cancellation request for a client service.</summary>
public sealed class CancelServiceHandler(
    IClientServiceRepository serviceRepo,
    ICancellationRequestRepository cancellationRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CancelServiceCommand"/>.
    /// Validates the service exists and no pending request already exists.
    /// </summary>
    /// <param name="cmd">The cancel command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or a pending cancellation request already exists.
    /// </exception>
    public async Task HandleAsync(CancelServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        var existing = await cancellationRepo.FindByServiceIdAsync(cmd.ServiceId, ct);
        if (existing is not null && existing.Status == CancellationStatus.Open)
        {
            throw new InvalidOperationException(
                $"A pending cancellation request already exists for service {cmd.ServiceId}.");
        }

        var cancellationType = Enum.Parse<CancellationType>(cmd.Type, ignoreCase: true);
        var request = CancellationRequest.Create(service.Id, cancellationType, cmd.Reason);
        cancellationRepo.Add(request);
        await uow.SaveChangesAsync(ct);
    }
}
