using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CancelService;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.CancelMyService;

/// <summary>
/// Raises a cancellation request against one of the calling client's own services, refusing every
/// service that is not theirs.
/// </summary>
/// <remarks>
/// <para>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// cancel a service through <see cref="CancelMyServiceCommand"/> without it having run.
/// </para>
/// <para>
/// Ownership is settled <b>before</b> <see cref="CancelServiceCommand"/> is dispatched, so a
/// stranger's id never reaches the database read inside that handler, which would otherwise
/// refuse an id that does not exist and succeed for one that does, and so tell the two apart.
/// </para>
/// </remarks>
public sealed class CancelMyServiceHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public CancelMyServiceHandler(IServiceOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, when no such
    /// service exists, or when the caller has no client record, or
    /// <c>CANCELLATION_ALREADY_PENDING</c> when an open request already exists for the service.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(CancelMyServiceCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(new CancelServiceCommand(cmd.ServiceId, cmd.Type, cmd.Reason), ct);
    }
}
