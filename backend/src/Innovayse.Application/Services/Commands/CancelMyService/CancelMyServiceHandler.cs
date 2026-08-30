namespace Innovayse.Application.Services.Commands.CancelMyService;

using Innovayse.Application.Services.Commands.CancelService;
using Innovayse.Application.Services.Common;
using Wolverine;

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
/// stranger's id never reaches the repository read inside that handler -- which would otherwise
/// refuse an id that does not exist and succeed for one that does, and so tell the two apart.
/// </para>
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own services.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class CancelMyServiceHandler(IServiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="CancelMyServiceCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the cancellation request has been recorded.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(CancelMyServiceCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        await bus.InvokeAsync(new CancelServiceCommand(cmd.ServiceId, cmd.Type, cmd.Reason), ct);
    }
}
