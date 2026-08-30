namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.API.RateLimiting;
using Innovayse.Application.Auth.Commands.CompleteSetup;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

/// <summary>
/// Auth endpoints remaining after SSO migration.
/// Login/Register/Refresh/Logout are handled by the Nuxt BFF via SSO directly.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="identity">Where this deployment's people are read from.</param>
/// <param name="roles">Local role store; roles are granted here, not by the SSO.</param>
/// <param name="authMode">Which sign-in mechanism this deployment runs.</param>
/// <param name="caller">
/// Who is calling. Reading it through the port rather than off <c>User</c> keeps
/// <c>ClaimsPrincipal</c> to the one adapter that implements it, and keeps the two spellings
/// of the subject claim — SSO mode's "sub", local mode's mapped NameIdentifier — in one place
/// instead of in every controller that needs the answer.
/// </param>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IMessageBus bus,
    IIdentityProvider identity,
    ISubjectRoleStore roles,
    IAuthModeProvider authMode,
    ICurrentRequestContext caller) : ControllerBase
{
    /// <summary>
    /// Returns how this deployment signs people in, so a browser client can offer the
    /// right control instead of guessing.
    /// </summary>
    /// <remarks>
    /// The admin SPA has no other way to know. It was rewritten to rely on the cookie
    /// session the OIDC exchange issues, and that exchange only runs under
    /// <c>Auth:Mode=sso</c> — so under <c>local</c> it showed an SSO button with nothing
    /// behind it. Reporting the mode lets it show a password form there instead.
    ///
    /// Anonymous on purpose: a caller has to read this before it can authenticate, and
    /// the answer is a deployment shape rather than a secret. It says which mechanism is
    /// in use, nothing about who may use it.
    /// </remarks>
    /// <returns>200 with <c>{ mode }</c>, either <c>local</c> or <c>sso</c>.</returns>
    [HttpGet("mode")]
    [AllowAnonymous]
    public IActionResult Mode() =>
        Ok(new { mode = authMode.IsLocalMode ? "local" : "sso" });

    /// <summary>Returns whether initial admin setup is required (nobody holds Admin yet).</summary>
    /// <remarks>
    /// Asks whether anyone holds <see cref="Roles.Admin"/>, where it used to ask whether any
    /// local user row existed. The old question stopped meaning anything once a deployment
    /// could have people without having user rows: against an SSO it answered "no users" for
    /// a populated product and offered setup to whoever asked.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// It also reports whether a setup <b>token</b> is needed, which a client cannot work out
    /// for itself and must not guess: under <c>local</c> the claim is gated on the token this
    /// installation printed to its log, under <c>sso</c> it is not gated at all. A screen that
    /// guessed would either hide a field the request will be refused without, or ask an SSO
    /// operator for a token that does not exist.
    /// </para>
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <c>{ required, tokenRequired }</c>.</returns>
    [HttpGet("setup-required")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupRequiredAsync(CancellationToken ct)
    {
        var claimed = await roles.AnyHasRoleAsync(Roles.Admin, ct);
        var required = !claimed;

        // Deliberately says only *whether* a token is wanted, never anything about the token
        // itself — not its length, not whether one is currently outstanding. This endpoint is
        // anonymous, and it has to stay a statement about the deployment's shape rather than
        // about its secrets.
        return Ok(new { required, tokenRequired = required && authMode.IsLocalMode });
    }

    /// <summary>
    /// Initial setup: grants the Admin role to the authenticated caller who presents this
    /// installation's setup token. Only works while nobody holds Admin.
    /// </summary>
    /// <param name="command">The claim, carrying the setup token and nothing else.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <c>{ success = true }</c> once the role has been granted.</returns>
    /// <remarks>
    /// <para>
    /// This used to grant Admin to whichever authenticated caller asked first. On a standalone
    /// install that is reachable before its owner has finished configuring it — the normal
    /// shape of a self-hosted deployment — that was an account-takeover window: registration is
    /// public, so whoever registered and claimed first owned the installation. Under
    /// <c>Auth:Mode=local</c> the claim now also requires the token
    /// <c>SetupTokenSeeder</c> writes and the API logs on every boot while setup is
    /// outstanding.
    /// </para>
    /// <para>
    /// Under <c>sso</c> nothing about this changed: no token is issued, none is asked for, and
    /// the first authenticated caller still claims. That path is in production use and the
    /// people who can reach an authenticated endpoint there are already the ones the operator
    /// provisioned in the sign-on service.
    /// </para>
    /// <para>
    /// The command binds directly — there is no request DTO in front of it — and it carries no
    /// subject: who is claiming is read from the credential inside the handler, so holding the
    /// token cannot be used to make somebody else Admin.
    /// </para>
    /// </remarks>
    [HttpPost("setup")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> SetupAsync(
        [FromBody] CompleteSetupCommand? command, CancellationToken ct)
    {
        // The body is optional so that an SSO caller — where no token exists and the SPA sends
        // none — still reaches the handler instead of being refused by model binding. Under
        // local mode a missing body arrives as a null token and the handler refuses it, which
        // is the same answer for the same reason, in the one place that decides it.
        //
        // Every refusal this can produce — already claimed, wrong token, no subject — is a
        // typed exception ExceptionMiddleware turns into the same { error, code } body as the
        // rest of the API, in the caller's own language. Nothing is worded here.
        await bus.InvokeAsync(command ?? new CompleteSetupCommand(null), ct);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Returns whether the current user's email is confirmed.
    /// With SSO, email confirmation is managed by the SSO service.
    /// </summary>
    /// <returns>200 with <c>{ verified }</c>.</returns>
    /// <remarks>
    /// The claim is read through <see cref="ICurrentRequestContext"/> rather than here. Two
    /// actions on this controller answered the same question, and each spelled the comparison
    /// out for itself — which is how one of them came to accept "true" and "True" and nothing
    /// else, silently reporting an issuer that writes "TRUE" as unverified.
    /// </remarks>
    [HttpGet("email-verified")]
    [Authorize]
    public IActionResult EmailVerifiedAsync() =>
        Ok(new { verified = caller.IsEmailVerified });

    /// <summary>
    /// Returns the current SSO-authenticated user's email and local roles.
    /// Used by SPA clients (the admin panel) that cannot decode roles from the
    /// SSO-issued token itself, since roles are assigned locally in Hostpanel,
    /// not by the SSO service.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> MeAsync(CancellationToken ct)
    {
        var subject = caller.UserId;
        if (subject is null) return Unauthorized();

        // One lookup, where this used to try the SSO subject and then the local id in turn.
        // Both modes now name a person the same way — whatever the configured provider
        // calls them — so there is only one thing the claim can mean.
        var account = await identity.FindBySubjectAsync(subject, ct);
        if (account is null) return Unauthorized();

        var held = await roles.GetRolesAsync(subject, ct);

        return Ok(new { email = account.Email, roles = held, emailVerified = caller.IsEmailVerified });
    }

    /// <summary>
    /// Accepts an invitation. The invitation token is validated and the
    /// current SSO user is assigned the role defined in the invitation.
    /// The caller must be authenticated via SSO.
    /// </summary>
    [HttpPost("accept-invite")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> AcceptInviteAsync([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        try
        {
            // No subject travels on the command: the handler asks the credential itself, so a
            // valid invitation token cannot be redeemed on behalf of a different account.
            await bus.InvokeAsync(new AcceptInvitationCommand(request.Token), ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { success = true });
    }
}
