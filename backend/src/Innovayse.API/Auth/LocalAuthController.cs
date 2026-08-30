namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.API.RateLimiting;
using Innovayse.Application.Auth.Events;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Common.Options;
using Innovayse.Domain.Auth;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Services;
using Innovayse.Application.Resources;
using Innovayse.Domain.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Wolverine;

/// <summary>
/// Local authentication endpoints. Password sign-in, registration and the password flows
/// that go with it — every one of them meaningless where an SSO owns the accounts.
/// </summary>
/// <remarks>
/// The service that backs these is resolved per action rather than injected, because it is
/// not registered at all in SSO mode. Taking it as a constructor parameter would make every
/// route on this controller fail to resolve there — including the ones that answer 404 on
/// purpose, which would then answer 500 instead.
///
/// <para>
/// An earlier comment here claimed login and 2FA were "always active (needed by admin
/// panel)". They are not: the admin SPA signs in through the shared cookie session under
/// SSO mode and only falls back to this form under local mode, which is what
/// <c>GET /api/auth/mode</c> tells it. Left ungated, these two routes were the reason this
/// controller had to keep a dependency the mode it serves does not have.
/// </para>
/// </remarks>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class LocalAuthController(
    IServiceProvider services,
    IJwtService tokenService,
    IMessageBus bus,
    IOptions<ClientPortalOptions> clientPortal,
    IAuthModeProvider authMode,
    IStringLocalizer<ValidationMessages> localizer) : ControllerBase
{
    /// <summary>
    /// Whether this deployment owns its own people. Used to be its own inline, plain
    /// <c>==</c> comparison here — case-sensitive, unlike the equivalent checks elsewhere,
    /// so a configured value of <c>Local</c> made this controller disagree with the rest of
    /// the process about which mode it was in. Now shared through <see cref="IAuthModeProvider"/>.
    /// </summary>
    private bool IsLocalMode => authMode.IsLocalMode;

    /// <summary>
    /// The local user service, resolved only once a route has confirmed the mode.
    /// </summary>
    private IUserService Users => services.GetRequiredService<IUserService>();

    /// <summary>
    /// Authenticates a user with email and password.
    /// Returns an access token, or a pending token if 2FA is required.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var userService = Users;
        var user = await userService.FindByEmailAndPasswordAsync(request.Email, request.Password, ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid email or password." });

        // Check 2FA
        var is2FaEnabled = await userService.IsTwoFactorEnabledAsync(user.Value.Id, ct);
        if (is2FaEnabled)
        {
            // Return a pending token (just the user ID encrypted/encoded — simple approach)
            var pendingToken = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"2fa:{user.Value.Id}:{DateTime.UtcNow.Ticks}"));
            return Ok(new { twoFactorRequired = true, pendingToken });
        }

        await userService.UpdateLastLoginAsync(user.Value.Id, ct);
        var roles = await userService.GetRolesAsync(user.Value.Id, ct);
        var emailConfirmed = await userService.IsEmailConfirmedAsync(user.Value.Id, ct);

        var accessToken = tokenService.GenerateAccessToken(
            user.Value.Id, user.Value.Email, null, null, [.. roles], emailConfirmed);

        // The browser's copy goes into an httpOnly cookie it cannot read. The token stays in
        // the body as well, because this endpoint has a second class of caller — anything
        // scripted or machine-to-machine — and taking it out would break them for a browser
        // problem they do not have. See LocalSessionCookie for why this is the same answer the
        // SSO path already reached.
        LocalSessionCookie.Issue(HttpContext, accessToken);

        return Ok(new { accessToken, expiresIn = 900 });
    }

    /// <summary>
    /// Completes two-factor authentication using a pending token and a TOTP code.
    /// Returns an access token on success.
    /// </summary>
    [HttpPost("2fa-login")]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFactorLoginAsync([FromBody] TwoFactorLoginRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        // Decode pending token
        string userId;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.PendingToken));
            var parts = decoded.Split(':');
            if (parts.Length < 2 || parts[0] != "2fa") return Unauthorized(new { error = "Invalid pending token." });
            userId = parts[1];
        }
        catch
        {
            return Unauthorized(new { error = "Invalid pending token." });
        }

        var userService = Users;
        var valid = await userService.VerifyTwoFactorCodeAsync(userId, request.Code, ct);
        if (!valid) return Unauthorized(new { error = "Invalid 2FA code." });

        await userService.UpdateLastLoginAsync(userId, ct);
        var user = await userService.FindByIdAsync(userId, ct);
        if (user is null) return Unauthorized();

        var roles = await userService.GetRolesAsync(userId, ct);
        var accessToken = tokenService.GenerateAccessToken(
            userId, user.Value.Email, null, null, [.. roles]);

        // Same as the credential step above: the browser's copy goes into the httpOnly cookie.
        // A sign-in that finished at the second factor has to end in the same session shape as
        // one that did not, or an operator with 2FA enabled would be the only one still holding
        // a token in script-readable storage.
        LocalSessionCookie.Issue(HttpContext, accessToken);

        return Ok(new { accessToken, expiresIn = 900 });
    }

    /// <summary>
    /// Ends a local-mode browser session by clearing the httpOnly session cookie.
    /// </summary>
    /// <returns>200 once the cookie has been cleared.</returns>
    /// <remarks>
    /// <para>
    /// It exists because the credential is no longer something the browser can drop on its own.
    /// Signing out used to be <c>sessionStorage.removeItem</c>; a cookie the page cannot read is
    /// also a cookie the page cannot delete, so the server has to be asked.
    /// </para>
    /// <para>
    /// <b>Why <c>DELETE</c> and not <c>POST</c>.</b> Under SSO mode <c>MapInnovayseAuth</c> maps
    /// <c>POST /api/auth/logout</c>. A controller action on the same method and path would give
    /// the router two candidate endpoints for one request — an ambiguous match, which fails at
    /// request time rather than at startup, and only on the mode this route is not for. Proven,
    /// not assumed: a throwaway build with <c>[HttpPost("logout")]</c> answered 500
    /// <c>AmbiguousMatchException</c> under <c>Auth__Mode=sso</c>.
    /// </para>
    /// <para>
    /// The verb is what keeps the path free, exactly as it does for login: the SDK serves
    /// <c>GET /api/auth/login</c> (a redirect into the SSO) while this controller serves
    /// <c>POST /api/auth/login</c>, and the router tells them apart by method alone. Ending a
    /// session is a deletion, so <c>DELETE</c> is both the free verb and the accurate one.
    /// </para>
    /// <para>
    /// Anonymous, and answering 200 either way: clearing a cookie that was not there is not a
    /// failure, and requiring a valid session to end one would leave an operator whose token had
    /// already expired unable to sign out.
    /// </para>
    /// </remarks>
    [HttpDelete("logout")]
    [AllowAnonymous]
    public IActionResult LocalLogout()
    {
        if (!IsLocalMode) return NotFound();

        LocalSessionCookie.Clear(HttpContext);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Registers a new user account with email and password.
    /// Returns the newly created user's ID.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var userService = Users;
        try
        {
            var userId = await userService.CreateAsync(
                request.Email, request.Password, ct, request.FirstName, request.LastName);

            // A user row on its own is not an account anyone can use. Both of these went
            // missing when registration was rewritten to call IUserService directly: the
            // RegisterHandler this replaced assigned the role and published the event, and
            // CreateClientOnRegisterHandler — still there, still idempotent — was left
            // waiting for a message nobody sent. What a locally-registered user got was an
            // AppUser with no role and no Client record, which is 403 on every client
            // endpoint and 401 on their own profile. The SSO provisioning path in
            // UserService assigns the same role for the same reason.
            await userService.AddToRoleAsync(userId, Roles.Client, ct);
            await bus.PublishAsync(new ClientRegisteredIntegrationEvent(
                userId, request.Email, request.FirstName ?? string.Empty, request.LastName ?? string.Empty));

            return Ok(new { userId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Initiates a password reset flow by generating a reset token for the given email.
    /// Always returns 200 to avoid user enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] IEmailTemplateRepository templateRepo,
        [FromServices] IUnitOfWork uow,
        CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var userService = Users;
        var user = await userService.FindByEmailAsync(request.Email, ct);
        if (user is null) return Ok(); // Silent — no enumeration

        var token = await userService.GeneratePasswordResetTokenAsync(user.Value.Id, ct);

        await PasswordResetTemplateSeeder.EnsureSeededAsync(templateRepo, uow, ct);

        // Where the portal lives is a bound setting with its own validated default, not a
        // key read by string here — the fallback that used to sit on this line was a second,
        // silent copy of ClientPortalOptions.BaseUrl's own default and could drift from it.
        var clientBaseUrl = clientPortal.Value.BaseUrl;
        var resetLink = $"{clientBaseUrl}/client/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Value.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            user.Value.Email,
            PasswordResetTemplateSeeder.Slug,
            new { reset_link = resetLink }), ct);

        return Ok();
    }

    /// <summary>
    /// Resets a user's password using a previously issued reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var success = await Users.ResetPasswordWithTokenAsync(
            request.Email, request.Token, request.NewPassword, ct);
        return success ? Ok() : BadRequest(new { error = localizer["TokenInvalidOrExpired"].Value });
    }

    /// <summary>
    /// Confirms a user's email address using the token sent during registration.
    /// </summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailAsync([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var success = await Users.ConfirmEmailAsync(request.Email, request.Token, ct);
        return success ? Ok() : BadRequest(new { error = localizer["TokenInvalidOrExpired"].Value });
    }

    /// <summary>
    /// Refresh token endpoint — not implemented; short-lived tokens require re-login when expired.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult RefreshAsync()
    {
        // Refresh token handling would need a token store — out of scope for now
        // The BFF sets short-lived access tokens; the user re-logs when expired
        return Unauthorized(new { error = localizer["SessionExpired"].Value });
    }
}
