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

        var typeDisplay = request.Type switch
        {
            CancellationType.Immediate => "Immediate",
            CancellationType.EndOfBillingPeriod => "End of Billing Period",
            _ => request.Type.ToString(),
        };

        return new CancellationStatusDto(true, typeDisplay);
    }
}
