namespace Innovayse.Application.Admin.Users.Commands.SetUserPassword;

using Innovayse.Application.Auth.Interfaces;

/// <summary>Handles <see cref="SetUserPasswordCommand"/>.</summary>
/// <param name="provisioning">Writes people, where this deployment owns them.</param>
public sealed class SetUserPasswordHandler(IUserProvisioning provisioning)
{
    /// <summary>Sets the password.</summary>
    /// <param name="cmd">The subject and the new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    public Task HandleAsync(SetUserPasswordCommand cmd, CancellationToken ct)
        => provisioning.SetPasswordAsync(cmd.Id, cmd.Password, ct);
}
