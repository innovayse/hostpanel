namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Auth endpoints remaining after SSO migration.
/// Login/Register/Refresh/Logout are handled by the Nuxt BFF via SSO directly.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IMessageBus bus,
    IIdentityProvider identity,
    ISubjectRoleStore roles,
    IAuthModeProvider authMode) : ControllerBase
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
    [HttpGet("setup-required")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupRequiredAsync(CancellationToken ct)
    {
        var claimed = await roles.AnyHasRoleAsync(Roles.Admin, ct);
        return Ok(new { required = !claimed });
    }

    /// <summary>
    /// Initial setup: grants the Admin role to the first authenticated caller.
    /// The caller must already be signed in. Only works while nobody holds Admin.
    /// </summary>
    [HttpPost("setup")]
    [Authorize]
    public async Task<IActionResult> SetupAsync(CancellationToken ct)
    {
        if (await roles.AnyHasRoleAsync(Roles.Admin, ct))
            return Conflict(new { error = "Setup already completed." });

        var subject = Subject();
        if (subject is null) return Unauthorized();

        await roles.AddAsync(subject, Roles.Admin, ct);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Returns whether the current SSO user's email is confirmed.
    /// With SSO, email confirmation is managed by the SSO service.
    /// This endpoint reads the 'email_verified' claim from the token.
    /// </summary>
    [HttpGet("email-verified")]
    [Authorize]
    public IActionResult EmailVerifiedAsync()
    {
        var verified = User.FindFirst("email_verified")?.Value == "true"
            || User.FindFirst("email_verified")?.Value == "True";
        return Ok(new { verified });
    }

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
        var subject = Subject();
        if (subject is null) return Unauthorized();

        // One lookup, where this used to try the SSO subject and then the local id in turn.
        // Both modes now name a person the same way — whatever the configured provider
        // calls them — so there is only one thing the claim can mean.
        var account = await identity.FindBySubjectAsync(subject, ct);
        if (account is null) return Unauthorized();

        var held = await roles.GetRolesAsync(subject, ct);
        var verified = User.FindFirst("email_verified")?.Value is "true" or "True";

        return Ok(new { email = account.Email, roles = held, emailVerified = verified });
    }

    /// <summary>
    /// Accepts an invitation. The invitation token is validated and the
    /// current SSO user is assigned the role defined in the invitation.
    /// The caller must be authenticated via SSO.
    /// </summary>
    [HttpPost("accept-invite")]
    [Authorize]
    public async Task<IActionResult> AcceptInviteAsync([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        var subject = Subject();
        if (subject is null) return Unauthorized();

        try
        {
            await bus.InvokeAsync(new AcceptInvitationCommand(request.Token, subject), ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// The signed-in caller's subject, or null if the token carries none.
    /// </summary>
    /// <remarks>
    /// Both claim names, because the two modes deliver it differently. SSO mode sets
    /// MapInboundClaims = false, so "sub" survives as itself; local mode leaves the default
    /// mapping on, which renames it to NameIdentifier. Reading only "sub" found nothing
    /// under local and answered 401 — with a valid token, from an account holding Admin.
    /// </remarks>
    private string? Subject() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
