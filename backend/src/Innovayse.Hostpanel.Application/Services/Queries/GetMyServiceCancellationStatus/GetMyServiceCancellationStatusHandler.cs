using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceCancellationStatus;

/// <summary>
/// Reads the cancellation status of one of the calling client's own services, refusing every
/// service that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message. Without it
/// the underlying query is an oracle in its own right: it answers for any id at all and never
/// fails, so it reports on another account's service without even looking like a refusal that
/// went missing. Once ownership is settled the projection is the same one a staff-side read would
/// perform, so this dispatches <see cref="GetCancellationStatusQuery"/> rather than growing a
/// second copy that could drift from it.
/// </remarks>
public sealed class GetMyServiceCancellationStatusHandler
{
    /// <summary>The rule that says a client may only look at their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared read once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only look at their own services.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
    public GetMyServiceCancellationStatusHandler(IServiceOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Whether a cancellation request is open on the service, and of which type, or
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, when no such service
    /// exists, or when the caller has no client record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<CancellationStatusDto>> HandleAsync(
        GetMyServiceCancellationStatusQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<CancellationStatusDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        return await _bus.InvokeAsync<Result<CancellationStatusDto>>(
            new GetCancellationStatusQuery(query.ServiceId), ct);
    }
}
