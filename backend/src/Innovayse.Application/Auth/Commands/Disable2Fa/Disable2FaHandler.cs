namespace Innovayse.Application.Auth.Commands.Disable2Fa;

using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Handles <see cref="Disable2FaCommand"/>.
/// Clears the stored TOTP secret and marks 2FA as disabled in Identity.
/// </summary>
/// <param name="userService">Abstraction over Identity user management.</param>
public sealed class Disable2FaHandler(IUserService userService)
{
    /// <summary>
    /// Disables 2FA and removes the TOTP secret for the user.
    /// </summary>
    /// <param name="cmd">The command carrying the user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(Disable2FaCommand cmd, CancellationToken ct)
    {
        await userService.DisableTwoFactorAsync(cmd.UserId, ct);
    }
}
