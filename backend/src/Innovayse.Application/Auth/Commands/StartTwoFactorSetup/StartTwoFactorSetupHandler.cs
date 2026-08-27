namespace Innovayse.Application.Auth.Commands.StartTwoFactorSetup;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;

/// <summary>Issues a fresh TOTP secret and the URI an authenticator app scans.</summary>
/// <param name="twoFactor">Mode-specific two-factor implementation.</param>
public sealed class StartTwoFactorSetupHandler(ITwoFactorService twoFactor)
{
    /// <summary>Handles <see cref="StartTwoFactorSetupCommand"/>.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret and the <c>otpauth://</c> URI, or null when the account is gone.</returns>
    /// <remarks>
    /// Generating a secret does not switch two-factor on — <see cref="Innovayse.Application.Auth.Commands.EnableTwoFactor.EnableTwoFactorHandler"/> does,
    /// and only after a code proves the app holds the same secret. Enabling here would lock an
    /// account out of its own sign-in the moment the person closed the page without scanning.
    /// </remarks>
    public Task<TwoFactorSetupDto?> HandleAsync(StartTwoFactorSetupCommand cmd, CancellationToken ct) =>
        twoFactor.StartSetupAsync(cmd.UserId, ct);
}
