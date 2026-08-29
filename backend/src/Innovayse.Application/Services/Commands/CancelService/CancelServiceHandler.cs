namespace Innovayse.Application.Services.Commands.CancelService;

using Innovayse.Application.Common;
using Innovayse.Application.Resources;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Localization;

/// <summary>Creates a cancellation request for a client service.</summary>
/// <param name="serviceRepo">Client service repository, for the service being cancelled.</param>
/// <param name="cancellationRepo">Cancellation request repository, checked for an open request.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
public sealed class CancelServiceHandler(
    IClientServiceRepository serviceRepo,
    ICancellationRequestRepository cancellationRepo,
    IUnitOfWork uow,
    IStringLocalizer<ValidationMessages> localizer)
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
            ?? throw new InvalidOperationException(localizer["ServiceNotFound", cmd.ServiceId]);

        var existing = await cancellationRepo.FindByServiceIdAsync(cmd.ServiceId, ct);
        if (existing is not null && existing.Status == CancellationStatus.Open)
        {
            throw new InvalidOperationException(localizer["CancellationAlreadyPending", cmd.ServiceId]);
        }

        // Case-sensitive on purpose, matching CancelServiceValidator's ordinal
        // "Immediate" or "EndOfBillingPeriod" exactly. The two used to disagree -- the validator
        // ordinal, this parse ignoreCase -- which meant the only thing keeping "immediate" out of
        // here was a validator that until recently ran not at all. They now agree in one
        // direction: the strict one. Nothing a caller can send changes answer, because the
        // validator already refuses every spelling this line newly would; what changes is that
        // the handler no longer depends on the validator to be the strict half.
        //
        // Strict is the right side to converge on because Type is a machine token, not prose:
        // GetCancellationStatusHandler hands back this exact spelling, so a caller echoing what
        // it read always matches, and accepting "IMMEDIATE" would only invite a spelling that
        // works here and nowhere else.
        var cancellationType = Enum.Parse<CancellationType>(cmd.Type);
        var request = CancellationRequest.Create(service.Id, cancellationType, cmd.Reason);
        cancellationRepo.Add(request);
        await uow.SaveChangesAsync(ct);
    }
}
