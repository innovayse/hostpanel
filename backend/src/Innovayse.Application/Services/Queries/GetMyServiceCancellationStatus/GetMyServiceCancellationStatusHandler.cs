namespace Innovayse.Application.Services.Queries.GetMyServiceCancellationStatus;

using Innovayse.Application.Services.Common;
using Innovayse.Application.Services.Queries.GetCancellationStatus;
using Wolverine;

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
/// <param name="ownership">The rule that says a client may only look at their own services.</param>
/// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
public sealed class GetMyServiceCancellationStatusHandler(IServiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyServiceCancellationStatusQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether a cancellation request is open on the service, and of which type.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<CancellationStatusDto> HandleAsync(
        GetMyServiceCancellationStatusQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        return await bus.InvokeAsync<CancellationStatusDto>(
            new GetCancellationStatusQuery(query.ServiceId), ct);
    }
}
