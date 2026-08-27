namespace Innovayse.Application.Auth.Commands.EnableTwoFactor;

using Innovayse.Application.Auth.Interfaces;

/// <summary>Turns two-factor authentication on after verifying a code.</summary>
/// <param name="twoFactor">Mode-specific two-factor implementation.</param>
public sealed class EnableTwoFactorHandler(ITwoFactorService twoFactor)
{
    /// <summary>Handles <see cref="EnableTwoFactorCommand"/>.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was switched on; false when the code did not match.</returns>
    /// <remarks>
    /// The code is checked against the secret this account was just issued, so a wrong one means
    /// the app is not holding that secret. Switching two-factor on regardless is how an account
    /// ends up demanding a code nothing can produce.
    /// </remarks>
    public Task<bool> HandleAsync(EnableTwoFactorCommand cmd, CancellationToken ct) =>
        twoFactor.EnableAsync(cmd.UserId, cmd.Code, ct);
}
