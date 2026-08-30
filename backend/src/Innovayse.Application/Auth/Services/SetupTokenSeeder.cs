namespace Innovayse.Application.Auth.Services;

using System.Security.Cryptography;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Issues and retires the one-time token that guards first-run setup on a standalone install.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for.</b> <c>POST /api/auth/setup</c> grants the Admin role, and until this
/// existed it granted it to whichever authenticated caller asked first. On a box that is
/// reachable from the internet before its owner has finished setting it up — which is the
/// normal shape of a self-hosted install: put it behind a domain, then configure it — anyone
/// could register through the public <c>POST /api/auth/register</c> and claim the
/// installation. The token moves the decision from "who asked first" to "who can read the
/// server's own log", which is a capability an operator has and a passer-by does not.
/// </para>
/// <para>
/// <b>Why not the other two options.</b> <i>Refusing setup once any user exists</i> is worse
/// than the race it closes: an attacker only has to register to make the installation
/// permanently unclaimable, turning an account takeover into an unrecoverable denial of
/// service. <i>Binding setup to the first account created</i> moves the race one step earlier
/// without closing it — the attacker who would have claimed first simply registers first, and
/// on a fresh box that is the same window.
/// </para>
/// <para>
/// <b>It cannot lock out an owner who restarts mid-setup.</b> The token is a database row, not
/// a process-lifetime value, so a restart re-reads the same one rather than issuing a new one
/// that invalidates what the operator already copied. The caller re-logs it on every boot for
/// as long as setup is outstanding, so an operator who lost the scrollback gets it back by
/// restarting the container — and an operator who never saw it gets it the same way.
/// </para>
/// <para>
/// <b>Local mode only.</b> Under <c>Auth:Mode=sso</c> this is never called, no row is written
/// and <c>CompleteSetupHandler</c> asks for no token: accounts there belong to the sign-on
/// service, so the set of callers who can reach an authenticated endpoint is already the set
/// the operator provisioned. The SSO path is in production use and this must not change it.
/// </para>
/// <para>
/// <b>It is not <c>DevDataSeeder</c>.</b> That one is gated to Development and seeds the demo
/// credentials published in the README, correctly. This runs in every environment and creates
/// no account at all.
/// </para>
/// </remarks>
public static class SetupTokenSeeder
{
    /// <summary>
    /// Settings key the outstanding setup token is stored under.
    /// <para>
    /// The settings table is used rather than a file or a new table because it is already the
    /// one thing on this deployment that survives a container restart, is inside the backup the
    /// deploy job takes, and is reachable from the Application layer through a port. The row is
    /// left in place with an <b>empty value</b> once setup completes rather than deleted:
    /// <c>ISettingRepository</c> exposes no removal, an empty value is refused by
    /// <c>CompleteSetupHandler</c> the same as a missing one, and the row then records that this
    /// installation was bootstrapped through the gate.
    /// </para>
    /// </summary>
    public const string SettingKey = "auth:setup-token";

    /// <summary>Description written on the settings row, for an operator reading the table.</summary>
    private const string SettingDescription =
        "One-time token required by POST /api/auth/setup to claim the Admin role on a standalone " +
        "install. Printed to the API log on every boot while setup is outstanding, and blanked once " +
        "it has been used. Not a credential for anything else.";

    /// <summary>Bytes of entropy behind the token, before base64url encoding.</summary>
    /// <remarks>
    /// 32 bytes — 256 bits. The token is rate-limited and single-use, so this is far more than
    /// guessing resistance needs; it is sized so that nobody has to reason about whether it is
    /// enough, and it still fits on one log line an operator can copy.
    /// </remarks>
    private const int TokenBytes = 32;

    /// <summary>
    /// Ensures a setup token exists while the Admin role is unclaimed, and returns it so the
    /// composition root can log it.
    /// </summary>
    /// <param name="settings">Setting repository.</param>
    /// <param name="roles">Role store, asked whether anyone already holds Admin.</param>
    /// <param name="uow">Unit of work for persisting a newly issued token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The outstanding setup token, or <see langword="null"/> when setup is already complete and
    /// there is nothing to issue or announce.
    /// </returns>
    /// <remarks>
    /// The token is returned rather than logged here on purpose: the Application layer takes no
    /// logging dependency, and the composition root is the only place that knows this line has
    /// to be legible in a <c>docker logs</c> tail rather than structured for a log sink.
    /// </remarks>
    public static async Task<string?> EnsureIssuedAsync(
        ISettingRepository settings,
        ISubjectRoleStore roles,
        IUnitOfWork uow,
        CancellationToken ct = default)
    {
        // Asked first, so a completed installation neither issues a token nor keeps announcing
        // one. It is also what retires an existing row: an installation bootstrapped before this
        // gate existed has no row at all, and one bootstrapped through it has already been
        // blanked by the handler — either way nothing is written here.
        if (await roles.AnyHasRoleAsync(Roles.Admin, ct))
        {
            return null;
        }

        var existing = await settings.FindByKeyAsync(SettingKey, ct);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Value))
        {
            // Re-announced, not re-issued. Rotating on every boot would invalidate the token an
            // operator had already copied, so restarting the container mid-setup would lock them
            // out of the step they were halfway through.
            return existing.Value;
        }

        var token = GenerateToken();

        if (existing is null)
        {
            settings.Add(Setting.Create(SettingKey, token, SettingDescription));
        }
        else
        {
            // The row exists with an empty value on exactly one path: setup was completed and
            // then the Admin role was revoked from every holder. Re-arming it is correct —
            // the installation is unclaimed again and needs a way back in.
            existing.UpdateValue(token);
        }

        await uow.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>
    /// Generates a fresh setup token.
    /// </summary>
    /// <returns>A base64url-encoded, cryptographically random token with no padding.</returns>
    /// <remarks>
    /// base64url rather than base64 so the value survives being pasted through a URL, a shell
    /// or a query string without escaping — an operator will copy this out of a terminal, and a
    /// <c>+</c> or <c>/</c> that silently changes on the way is a support ticket nobody can
    /// diagnose from the token being "wrong".
    /// </remarks>
    private static string GenerateToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>
    /// Encodes bytes as unpadded base64url.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <returns>The base64url text.</returns>
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
