namespace Innovayse.Application.Auth.Commands.DisableTwoFactor;

using Innovayse.Application.Auth.Interfaces;

/// <summary>Turns two-factor authentication off after verifying a code.</summary>
/// <param name="twoFactor">Mode-specific two-factor implementation.</param>
public sealed class DisableTwoFactorHandler(ITwoFactorService twoFactor)
{
    /// <summary>Handles <see cref="DisableTwoFactorCommand"/>.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was switched off; false when the code did not match.</returns>
    /// <remarks>
    /// A code is required to switch it off, not just a signed-in session, and that is the whole
    /// point of the feature: someone who has taken over a session could otherwise remove the
    /// second factor that was meant to stop them. The SSO in this workspace asks for one on its
    /// own disable path for the same reason.
    /// </remarks>
    public Task<bool> HandleAsync(DisableTwoFactorCommand cmd, CancellationToken ct) =>
        twoFactor.DisableAsync(cmd.UserId, cmd.Code, ct);
}
