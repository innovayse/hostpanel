namespace Innovayse.Application.Admin.Users.Commands.UpdateUser;

using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Handles <see cref="UpdateUserCommand"/>: renames the person, then moves their sign-in
/// address.
/// </summary>
/// <param name="provisioning">Writes people, where this deployment owns them.</param>
public sealed class UpdateUserHandler(IUserProvisioning provisioning)
{
    /// <summary>Applies both profile writes.</summary>
    /// <param name="cmd">The subject and the new profile fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    public async Task HandleAsync(UpdateUserCommand cmd, CancellationToken ct)
    {
        // Two writes, because they are two different things: a rename, and a change to the
        // address someone signs in with. Both refuse together where an SSO owns the person,
        // so neither can land without the other.
        await provisioning.UpdateProfileAsync(cmd.Id, cmd.FirstName, cmd.LastName, cmd.Language, ct);
        await provisioning.ChangeEmailAsync(cmd.Id, cmd.Email, ct);
    }
}
