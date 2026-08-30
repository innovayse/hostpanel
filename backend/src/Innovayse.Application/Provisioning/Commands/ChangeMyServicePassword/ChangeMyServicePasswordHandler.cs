namespace Innovayse.Application.Provisioning.Commands.ChangeMyServicePassword;

using Innovayse.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Application.Services.Common;
using Wolverine;

/// <summary>
/// Changes the hosting account password of one of the calling client's own services, refusing
/// every service that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// reset a hosting password through <see cref="ChangeMyServicePasswordCommand"/> without it
/// having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="ChangePasswordCommand"/> rather than duplicating it; that shared command
/// stays unscoped because an administrator resetting any client's password is legitimate.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own services.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class ChangeMyServicePasswordHandler(IServiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="ChangeMyServicePasswordCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the password has been changed.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(ChangeMyServicePasswordCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        await bus.InvokeAsync(new ChangePasswordCommand(cmd.ServiceId, cmd.NewPassword), ct);
    }
}
