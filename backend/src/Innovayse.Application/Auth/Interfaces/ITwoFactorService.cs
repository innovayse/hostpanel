namespace Innovayse.Application.Auth.Interfaces;

using Innovayse.Application.Auth.Common;


/// <summary>
/// Two-factor authentication for the signed-in account, split from the other 15+ unrelated
/// members of <see cref="IUserService"/> that this feature never touches.
///
/// <para>
/// Where an SSO owns the accounts, <see cref="IUserService"/> is not registered at all —
/// so <see cref="Innovayse.Application.Auth.Commands.StartTwoFactorSetup.StartTwoFactorSetupHandler"/>,
/// <see cref="Innovayse.Application.Auth.Commands.EnableTwoFactor.EnableTwoFactorHandler"/> and
/// <see cref="Innovayse.Application.Auth.Commands.DisableTwoFactor.DisableTwoFactorHandler"/>,
/// which took it as a constructor dependency, could never be constructed by Wolverine there.
/// Every call to any of the three <c>/api/me/2fa/*</c> routes failed dependency resolution —
/// a 500 — in the mode this product actually runs in. This interface exists so both modes
/// have something registered: the local implementation delegates to the existing
/// <see cref="IUserService"/> TOTP methods unchanged, and the SSO implementation forwards the
/// caller's own bearer token to the SSO's own TOTP endpoints, since two-factor is a
/// self-service action on the signed-in person's own account.
/// </para>
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Issues a fresh TOTP secret and the URI an authenticator app scans, for the given
    /// account.
    /// </summary>
    /// <param name="userId">Whose account. Comes from the credential, never from a request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret and enrolment URI, or null when the account does not exist.</returns>
    Task<TwoFactorSetupDto?> StartSetupAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Switches two-factor authentication on, once <paramref name="code"/> proves the
    /// authenticator app holds the secret issued by <see cref="StartSetupAsync"/>.
    /// </summary>
    /// <param name="userId">Whose account. Comes from the credential, never from a request body.</param>
    /// <param name="code">The six digits the authenticator app currently shows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was switched on; false when the code did not match.</returns>
    Task<bool> EnableAsync(string userId, string code, CancellationToken ct);

    /// <summary>
    /// Switches two-factor authentication off, once <paramref name="code"/> proves the
    /// caller still holds the second factor.
    /// </summary>
    /// <param name="userId">Whose account. Comes from the credential, never from a request body.</param>
    /// <param name="code">A current code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was switched off; false when the code did not match.</returns>
    Task<bool> DisableAsync(string userId, string code, CancellationToken ct);
}
