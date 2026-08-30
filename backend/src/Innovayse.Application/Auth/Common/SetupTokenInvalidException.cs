namespace Innovayse.Application.Auth.Common;

/// <summary>
/// Thrown when first-run setup is attempted without the setup token this installation issued,
/// or with the wrong one.
/// </summary>
/// <remarks>
/// <para>
/// The refusal exists because "the first caller to reach <c>POST /api/auth/setup</c> becomes
/// Admin" is a race, and on a standalone box that is reachable from the internet before its
/// owner has finished configuring it, that race is an account-takeover window: anyone may
/// register, and whoever registers and claims first owns the installation.
/// </para>
/// <para>
/// <b>Local mode only.</b> Under <c>Auth:Mode=sso</c> no token is issued and none is asked
/// for — accounts live in the sign-on service, so the population that can reach an
/// authenticated endpoint at all is already the population the operator provisioned. Adding a
/// gate there would change a path that is in production use and is not the one this fixes.
/// </para>
/// <para>
/// It is answered <b>403</b>, not 401: the caller's credential was accepted — they are signed
/// in — and it is the claim itself that is refused. A 401 would tell the admin SPA to send
/// them back to sign in, which would not help and would loop.
/// </para>
/// </remarks>
public sealed class SetupTokenInvalidException() : Exception("The setup token is missing or does not match.")
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// It crosses the wire, so it must not be reworded.
    /// </summary>
    public const string Code = "SETUP_TOKEN_INVALID";

    /// <summary>
    /// Key of this refusal's sentence in
    /// <c>Innovayse.Application/Resources/ValidationMessages.resx</c> and its <c>.ru</c> /
    /// <c>.hy</c> siblings.
    /// </summary>
    public const string MessageKey = "SetupTokenInvalid";
}
