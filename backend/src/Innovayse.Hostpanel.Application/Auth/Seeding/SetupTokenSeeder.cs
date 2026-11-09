using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Application.Auth.Seeding;

/// <summary>
/// Issues and retires the one-time token that guards first-run setup.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for.</b> <c>POST /api/auth/setup</c> grants the Admin role, and until this
/// existed it granted it to whichever authenticated caller asked first. On a box that is
/// reachable from the internet before its owner has finished setting it up, which is the
/// normal shape of a self-hosted install: put it behind a domain, then configure it, anyone
/// could register through the public <c>POST /api/auth/register</c> and claim the
/// installation. The token moves the decision from "who asked first" to "who can read the
/// server's own log", which is a capability an operator has and a passer-by does not.
/// </para>
/// <para>
/// <b>Why not the other two options.</b> <i>Refusing setup once any user exists</i> is worse
/// than the race it closes: an attacker only has to register to make the installation
/// permanently unclaimable, turning an account takeover into an unrecoverable denial of
/// service. <i>Binding setup to the first account created</i> moves the race one step earlier
/// without closing it: the attacker who would have claimed first simply registers first, and
/// on a fresh box that is the same window.
/// </para>
/// <para>
/// <b>It cannot lock out an owner who restarts mid-setup.</b> The token is a database row, not
/// a process-lifetime value, so a restart re-reads the same one rather than issuing a new one
/// that invalidates what the operator already copied. The caller re-logs it on every boot for
/// as long as setup is outstanding, so an operator who lost the scrollback gets it back by
/// restarting the container, and an operator who never saw it gets it the same way.
/// </para>
/// <para>
/// <b>Local mode only.</b> Under <c>Auth:Mode=sso</c> this is never called, no row is written
/// and <c>CompleteSetupHandler</c> asks for no token: accounts there belong to the sign-on
/// service, so the set of callers who can reach an authenticated endpoint is already the set
/// the operator provisioned.
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
    /// deploy job takes, and is reachable from the Application layer through
    /// <see cref="ISetupTokenStore"/>, since the settings belong to the Admin module. The row is
    /// left in place with an <b>empty value</b> once setup completes rather than deleted:
    /// an empty value is refused by
    /// <c>CompleteSetupHandler</c> the same as a missing one, and the row then records that this
    /// installation was bootstrapped through the gate.
    /// </para>
    /// </summary>
    public const string SettingKey = SecretSettingKeys.SetupToken;

    /// <summary>Description written on the settings row, for an operator reading the table.</summary>
    public const string SettingDescription =
        "One-time token required by POST /api/auth/setup to claim the Admin role. " +
        "install. Printed to the API log on every boot while setup is outstanding, and blanked once " +
        "it has been used. Not a credential for anything else.";

    /// <summary>Bytes of entropy behind the token, before base64url encoding.</summary>
    /// <remarks>
    /// 32 bytes: 256 bits. The token is rate-limited and single-use, so this is far more than
    /// guessing resistance needs; it is sized so that nobody has to reason about whether it is
    /// enough, and it still fits on one log line an operator can copy.
    /// </remarks>
    private const int TokenBytes = 32;

    /// <summary>
    /// Ensures a setup token exists while the Admin role is unclaimed, and returns it so the
    /// composition root can log it.
    /// </summary>
    /// <param name="store">The setup token store, which keeps the token among the settings.</param>
    /// <param name="roles">Role store, asked whether anyone already holds Admin.</param>
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
        ISetupTokenStore store,
        ISubjectRoleStore roles,
        CancellationToken ct = default)
    {
        // Asked first, so a completed installation neither issues a token nor keeps announcing
        // one. It is also what retires an existing row: an installation bootstrapped before this
        // gate existed has no row at all, and one bootstrapped through it has already been
        // blanked by the handler; either way nothing is written here.
        if (await roles.AnyHasRoleAsync(Role.Admin, ct))
        {
            return null;
        }

        // Re-announced, not re-issued: the store keeps an outstanding token and only stores this
        // fresh one when none is outstanding. Rotating on every boot would invalidate the token an
        // operator had already copied, so restarting the container mid-setup would lock them out
        // of the step they were halfway through. A row left blank by a completed setup is re-armed:
        // the Admin role was revoked from every holder, so the installation is unclaimed again.
        return await store.GetOrIssueAsync(GenerateToken(), ct);
    }

    /// <summary>
    /// Generates a fresh setup token.
    /// </summary>
    /// <returns>A base64url-encoded, cryptographically random token with no padding.</returns>
    /// <remarks>
    /// base64url rather than base64 so the value survives being pasted through a URL, a shell
    /// or a query string without escaping: an operator will copy this out of a terminal, and a
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
