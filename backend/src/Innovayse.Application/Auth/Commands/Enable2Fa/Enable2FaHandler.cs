namespace Innovayse.Application.Auth.Commands.Enable2Fa;

using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Handles <see cref="Enable2FaCommand"/>.
/// Verifies the TOTP code against the user's stored secret and, if valid, marks 2FA as enabled.
/// </summary>
/// <param name="userService">Abstraction over Identity user management.</param>
public sealed class Enable2FaHandler(IUserService userService)
{
    /// <summary>
    /// Verifies the TOTP code and enables 2FA on the account.
    /// </summary>
    /// <param name="cmd">The command carrying the user ID and TOTP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the provided TOTP code is invalid.</exception>
    public async Task HandleAsync(Enable2FaCommand cmd, CancellationToken ct)
    {
        var valid = await userService.VerifyTwoFactorCodeAsync(cmd.UserId, cmd.Code, ct);
        if (!valid)
        {
            throw new InvalidOperationException("Invalid TOTP code. Please try again.");
        }

        await userService.EnableTwoFactorAsync(cmd.UserId, ct);
    }
}
