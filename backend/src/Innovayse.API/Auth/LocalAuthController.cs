namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Local authentication endpoints.
/// Login/2FA are always active (needed by admin panel).
/// Registration and password flows are only active when Auth:Mode=local.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class LocalAuthController(
    IUserService userService,
    JwtTokenService tokenService,
    IMessageBus bus,
    IConfiguration config) : ControllerBase
{
    private bool IsLocalMode => (config["Auth:Mode"] ?? "sso") == "local";

    /// <summary>
    /// Authenticates a user with email and password.
    /// Returns an access token, or a pending token if 2FA is required.
    /// Always active — the admin panel uses local auth regardless of Auth:Mode.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {

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
            user.Value.Id, user.Value.Email, null, null, roles, emailConfirmed);

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

        var valid = await userService.VerifyTwoFactorCodeAsync(userId, request.Code, ct);
        if (!valid) return Unauthorized(new { error = "Invalid 2FA code." });

        await userService.UpdateLastLoginAsync(userId, ct);
        var user = await userService.FindByIdAsync(userId, ct);
        if (user is null) return Unauthorized();

        var roles = await userService.GetRolesAsync(userId, ct);
        var accessToken = tokenService.GenerateAccessToken(
            userId, user.Value.Email, null, null, roles);

        return Ok(new { accessToken, expiresIn = 900 });
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

        try
        {
            var userId = await userService.CreateAsync(
                request.Email, request.Password, ct, request.FirstName, request.LastName);
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

        var user = await userService.FindByEmailAsync(request.Email, ct);
        if (user is null) return Ok(); // Silent — no enumeration

        var token = await userService.GeneratePasswordResetTokenAsync(user.Value.Id, ct);

        await PasswordResetTemplateSeeder.EnsureSeededAsync(templateRepo, uow, ct);

        var clientBaseUrl = config["ClientBaseUrl"] ?? "http://localhost:3000";
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

        var success = await userService.ResetPasswordWithTokenAsync(
            request.Email, request.Token, request.NewPassword, ct);
        return success ? Ok() : BadRequest(new { error = "Invalid or expired token." });
    }

    /// <summary>
    /// Confirms a user's email address using the token sent during registration.
    /// </summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailAsync([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        if (!IsLocalMode) return NotFound();

        var success = await userService.ConfirmEmailAsync(request.Email, request.Token, ct);
        return success ? Ok() : BadRequest(new { error = "Invalid or expired token." });
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
        return Unauthorized(new { error = "Token expired. Please log in again." });
    }

    // Request DTOs not already in Requests/

    /// <summary>Request body for POST /api/auth/forgot-password.</summary>
    /// <param name="Email">The user's email address.</param>
    public record ForgotPasswordRequest(string Email);

    /// <summary>Request body for POST /api/auth/confirm-email.</summary>
    /// <param name="Email">The user's email address.</param>
    /// <param name="Token">The email confirmation token.</param>
    public record ConfirmEmailRequest(string Email, string Token);
}
