namespace Innovayse.Application.Admin.Users.Commands.DeleteUser;

using Innovayse.Application.Auth.Interfaces;

/// <summary>Handles <see cref="DeleteUserCommand"/>.</summary>
/// <param name="provisioning">Writes people, where this deployment owns them.</param>
public sealed class DeleteUserHandler(IUserProvisioning provisioning)
{
    /// <summary>Deletes the account. Records that reference the subject are left alone.</summary>
    /// <param name="cmd">The subject to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    public Task HandleAsync(DeleteUserCommand cmd, CancellationToken ct)
        => provisioning.DeleteAsync(cmd.Id, ct);
}
