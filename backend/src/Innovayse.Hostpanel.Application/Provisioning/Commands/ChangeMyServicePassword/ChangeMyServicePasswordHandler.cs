using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.ChangeMyServicePassword;

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
public sealed class ChangeMyServicePasswordHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="ChangeMyServicePasswordHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public ChangeMyServicePasswordHandler(IServiceOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ChangeMyServicePasswordCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, when no such
    /// service exists, or when the caller has no client record, or <c>SERVICE_NOT_PROVISIONED</c>,
    /// <c>SERVICE_NOT_ACTIVE</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c> or <c>SERVICE_PASSWORD_UPSTREAM_FAILED</c> from the
    /// shared use case.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(ChangeMyServicePasswordCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(new ChangePasswordCommand(cmd.ServiceId, cmd.NewPassword), ct);
    }
}
