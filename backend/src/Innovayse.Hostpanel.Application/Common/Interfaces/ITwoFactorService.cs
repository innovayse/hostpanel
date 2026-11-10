using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Two-factor authentication for the signed-in account, split from the other 15+ unrelated
/// members of <see cref="IUserService"/> that this feature never touches.
///
/// <para>
/// Where an external sign-on provider owns the accounts, <see cref="IUserService"/> is not registered at all:
/// so <see cref="Innovayse.Hostpanel.Application.Auth.Commands.StartTwoFactorSetup.StartTwoFactorSetupHandler"/>,
/// <see cref="Innovayse.Hostpanel.Application.Auth.Commands.EnableTwoFactor.EnableTwoFactorHandler"/> and
/// <see cref="Innovayse.Hostpanel.Application.Auth.Commands.DisableTwoFactor.DisableTwoFactorHandler"/>,
/// which took it as a constructor dependency, could never be constructed by Wolverine there.
/// Every call to any of the three <c>/api/me/2fa/*</c> routes failed dependency resolution,
/// a 500, in the mode this product actually runs in. This interface exists so both modes
/// have something registered: the local implementation delegates to the existing
/// <see cref="IUserService"/> TOTP methods unchanged, and the sign-on provider implementation forwards the
/// caller's own bearer token to the sign-on provider's own TOTP endpoints, since two-factor is a
/// self-service action on the signed-in person's own account.
/// </para>
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Issues a fresh TOTP secret and the URI an authenticator app scans, for the given account.
    /// The secret stays pending until <see cref="EnableAsync"/> confirms it.
    /// </summary>
    /// <param name="userId">Whose account. Comes from the credential, never from a request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The secret and enrolment URI; <c>UNAUTHORIZED</c> when the account does not exist; or
    /// <c>TWO_FACTOR_ALREADY_ENABLED</c> when two-factor is already on, so a setup can never replace
    /// the secret an enabled account signs in with.
    /// </returns>
    public Task<Result<TwoFactorSetupDto>> StartSetupAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Switches two-factor authentication on, once the code proves the authenticator app holds
    /// the secret issued by <see cref="StartSetupAsync"/>.
    /// </summary>
    /// <param name="confirmation">Whose account and the code the app currently shows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once it is on, or <c>TWO_FACTOR_CODE_INVALID</c> when the code did not match a pending secret.</returns>
    public Task<Result> EnableAsync(TwoFactorConfirmation confirmation, CancellationToken ct);

    /// <summary>
    /// Switches two-factor authentication off, once the caller proves they still hold the second
    /// factor and, where this deployment owns the accounts, the current password.
    /// </summary>
    /// <param name="removal">Whose account, a current code and the current password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once it is off, <c>CURRENT_PASSWORD_INVALID</c> when the password is missing or wrong,
    /// or <c>TWO_FACTOR_CODE_INVALID</c> when the code did not match.
    /// </returns>
    public Task<Result> DisableAsync(TwoFactorRemoval removal, CancellationToken ct);
}
