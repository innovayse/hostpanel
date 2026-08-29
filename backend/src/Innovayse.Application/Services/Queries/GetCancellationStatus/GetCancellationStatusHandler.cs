namespace Innovayse.Application.Services.Queries.GetCancellationStatus;

using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns the cancellation status for a given client service.</summary>
public sealed class GetCancellationStatusHandler(ICancellationRequestRepository cancellationRepo)
{
    /// <summary>
    /// Handles <see cref="GetCancellationStatusQuery"/>.
    /// Checks for an existing open cancellation request on the service.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cancellation status DTO.</returns>
    public async Task<CancellationStatusDto> HandleAsync(GetCancellationStatusQuery qry, CancellationToken ct)
    {
        var request = await cancellationRepo.FindByServiceIdAsync(qry.ServiceId, ct);

        if (request is null || request.Status != CancellationStatus.Open)
        {
            return new CancellationStatusDto(false, null);
        }

        // The enum member name, not a sentence. This field is read by the client portal, which
        // ships en/ru/hy and resolves the member name to one of its own i18n keys; English prose
        // baked in here cannot be translated. It is also the same spelling CancelServiceCommand
        // accepts on the way in, so what a caller reads back is what it may send.
        return new CancellationStatusDto(true, request.Type.ToString());
    }
}
