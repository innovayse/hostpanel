namespace Innovayse.Application.Services.Commands.SetupMyService;

using Innovayse.Application.Services.Commands.SetupService;
using Innovayse.Application.Services.Common;
using Wolverine;

/// <summary>
/// Sets up and provisions one of the calling client's own pending services, refusing every service
/// that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// provision through <see cref="SetupMyServiceCommand"/> without it having run. Refusing first
/// also keeps a stranger's id away from <see cref="SetupServiceCommand"/>, whose own refusals
/// distinguish "not found" from "not pending" and would otherwise report the state of somebody
/// else's service. Once ownership is settled the work is unchanged, so this dispatches the shared
/// command rather than duplicating provisioning.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own services.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class SetupMyServiceHandler(IServiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="SetupMyServiceCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the service has been provisioned and activated.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(SetupMyServiceCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        await bus.InvokeAsync(
            new SetupServiceCommand(cmd.ServiceId, cmd.Domain, cmd.Username, cmd.Password), ct);
    }
}
