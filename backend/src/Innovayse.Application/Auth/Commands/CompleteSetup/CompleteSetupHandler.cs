namespace Innovayse.Application.Auth.Commands.CompleteSetup;

using System.Security.Cryptography;
using System.Text;
using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Auth.Services;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="CompleteSetupCommand"/> — the first-run bootstrap that grants the Admin
/// role to the caller, once, and only to a caller who can prove they are the operator.
/// </summary>
/// <param name="roles">Role store: asked whether Admin is unclaimed, then written to.</param>
/// <param name="settings">Setting repository, holding the outstanding setup token.</param>
/// <param name="authMode">Which sign-in mechanism this deployment runs.</param>
/// <param name="uow">Unit of work for persisting the retired token.</param>
/// <param name="caller">Who is claiming; the command does not say, and must not.</param>
public sealed class CompleteSetupHandler(
    ISubjectRoleStore roles,
    ISettingRepository settings,
    IAuthModeProvider authMode,
    IUnitOfWork uow,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Grants Admin to the authenticated caller and retires the setup token.
    /// </summary>
    /// <param name="cmd">The command, carrying the setup token and nothing else.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the role has been granted.</returns>
    /// <exception cref="SetupAlreadyCompletedException">Thrown when somebody already holds Admin.</exception>
    /// <exception cref="SetupTokenInvalidException">
    /// Thrown under <c>Auth:Mode=local</c> when the request carried no setup token, the wrong
    /// one, or when this installation has no token outstanding to match against.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(CompleteSetupCommand cmd, CancellationToken ct)
    {
        // Asked before the token is even looked at, so a caller with the wrong token cannot use
        // the difference between the two refusals to learn whether the installation is still
        // claimable. The window this closes is small, but it is free to close.
        if (await roles.AnyHasRoleAsync(Roles.Admin, ct))
        {
            throw new SetupAlreadyCompletedException();
        }

        var subject = caller.UserId
            ?? throw new UnauthorizedAccessException("Setup requires an authenticated caller.");

        // Under sso the gate is inert: no token was ever issued, none is asked for, and this
        // path behaves exactly as it did before the gate existed. Accounts there belong to the
        // sign-on service, so the callers who can reach an authenticated endpoint at all are
        // already the ones the operator provisioned — and that path is in production use.
        if (authMode.IsLocalMode)
        {
            await ConsumeSetupTokenAsync(cmd.SetupToken, ct);
        }

        await roles.AddAsync(subject, Roles.Admin, ct);
    }

    /// <summary>
    /// Verifies the presented setup token against the outstanding one and blanks it.
    /// </summary>
    /// <param name="presented">The token the caller sent, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the token has been retired.</returns>
    /// <exception cref="SetupTokenInvalidException">Thrown when it is absent or does not match.</exception>
    private async Task ConsumeSetupTokenAsync(string? presented, CancellationToken ct)
    {
        var issued = await settings.FindByKeyAsync(SetupTokenSeeder.SettingKey, ct);

        // Fails closed. A missing or blank row means either that this installation has already
        // been bootstrapped through the gate, or that the boot-time issuer never ran — and in
        // neither case is granting Admin to whoever asked the safe reading. The sentence tells
        // the operator to restart the API, which re-issues and re-logs a token.
        if (issued is null || string.IsNullOrWhiteSpace(issued.Value) || string.IsNullOrEmpty(presented))
        {
            throw new SetupTokenInvalidException();
        }

        // Fixed-time comparison. The tokens are 256-bit random, so a timing oracle is not the
        // realistic attack here, but a byte-by-byte early exit on the one secret that guards
        // ownership of the installation is not worth defending in review.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(issued.Value),
            Encoding.UTF8.GetBytes(presented));

        if (!matches)
        {
            throw new SetupTokenInvalidException();
        }

        // Retired by blanking rather than deleting: ISettingRepository exposes no removal, an
        // empty value is refused above exactly as a missing row is, and the row left behind
        // records that this installation was bootstrapped through the gate. Saved here rather
        // than after the grant so that a failure between the two leaves the token spent rather
        // than the role granted and the token still live.
        issued.UpdateValue(string.Empty);
        await uow.SaveChangesAsync(ct);
    }
}
