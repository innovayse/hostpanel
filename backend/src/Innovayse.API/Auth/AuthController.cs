namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.Application.Auth.Commands.Login;
using Innovayse.Application.Auth.Commands.Logout;
using Innovayse.Application.Auth.Commands.RefreshToken;
using Innovayse.Application.Auth.Commands.Register;
using Innovayse.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Handles authentication — login, register, token refresh, and logout.
/// Access tokens are returned in the response body.
/// Refresh tokens are set/read as httpOnly cookies named <c>refreshToken</c>.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching commands.</param>
/// <param name="userService">User management service for setup checks.</param>
/// <param name="configuration">Application configuration for reading settings.</param>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMessageBus bus, IUserService userService, IConfiguration configuration) : ControllerBase
{
    /// <summary>Name of the httpOnly cookie that stores the refresh token.</summary>
    private const string RefreshTokenCookieName = "refreshToken";

    /// <summary>
    /// Authenticates a user and returns an access token.
    /// Sets the refresh token as an httpOnly cookie.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and expiry.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Innovayse.Application.Auth.DTOs.AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<AuthWithRefreshDto>(
            new LoginCommand(request.Email, request.Password), ct);

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(result.Auth);
    }

    /// <summary>
    /// Registers a new client account and returns an access token.
    /// Sets the refresh token as an httpOnly cookie.
    /// </summary>
    /// <param name="request">Registration data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and expiry.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Innovayse.Application.Auth.DTOs.AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<AuthWithRefreshDto>(
            new RegisterCommand(request.Email, request.Password, request.FirstName, request.LastName), ct);

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(result.Auth);
    }

    /// <summary>
    /// Exchanges the refresh token cookie for a new access token and rotated refresh token.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New access token and expiry.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Innovayse.Application.Auth.DTOs.AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized("Refresh token cookie missing.");
        }

        var result = await bus.InvokeAsync<AuthWithRefreshDto>(
            new RefreshTokenCommand(token), ct);

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(result.Auth);
    }

    /// <summary>
    /// Revokes the current refresh token and clears the cookie.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(token))
        {
            await bus.InvokeAsync(new LogoutCommand(token), ct);
        }

        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    /// <summary>
    /// Returns whether initial setup is required (no users exist in the system).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object with <c>required</c> boolean.</returns>
    [HttpGet("setup-required")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetupRequiredAsync(CancellationToken ct)
    {
        var anyUsers = await userService.AnyUsersExistAsync(ct);
        return Ok(new { required = !anyUsers });
    }

    /// <summary>
    /// Creates the initial admin account. Only works when no users exist.
    /// Sends a verification email to the admin's address.
    /// </summary>
    /// <param name="request">Registration data for the admin user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and expiry for the new admin.</returns>
    [HttpPost("setup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetupAsync([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var anyUsers = await userService.AnyUsersExistAsync(ct);
        if (anyUsers)
        {
            return Conflict(new { error = "Setup already completed. An admin account exists." });
        }

        var userId = await userService.CreateAsync(request.Email, request.Password, ct);
        await userService.AddToRoleAsync(userId, Roles.Admin, ct);

        var tokenService = HttpContext.RequestServices.GetRequiredService<ITokenService>();
        var refreshTokenRepo = HttpContext.RequestServices.GetRequiredService<IRefreshTokenRepository>();
        var uow = HttpContext.RequestServices.GetRequiredService<Application.Common.IUnitOfWork>();

        // Seed the email verification template if it doesn't exist
        var templateRepo = HttpContext.RequestServices.GetRequiredService<Domain.Notifications.Interfaces.IEmailTemplateRepository>();
        var existingTemplate = await templateRepo.FindBySlugAsync("admin-email-verification", ct);
        if (existingTemplate is null)
        {
            var emailBody = """
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                </head>
                <body style="margin:0;padding:0;background-color:#0a0a0f;font-family:'Inter',system-ui,-apple-system,sans-serif;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0f;padding:40px 20px;">
                    <tr>
                      <td align="center">
                        <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;">
                          <!-- Logo -->
                          <tr>
                            <td align="center" style="padding-bottom:32px;">
                              <table role="presentation" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="background:linear-gradient(135deg,rgba(14,165,233,0.1),rgba(168,85,247,0.1));border:1px solid rgba(14,165,233,0.2);border-radius:10px;padding:8px 16px;">
                                    <span style="font-size:16px;font-weight:700;background:linear-gradient(135deg,#0ea5e9,#a855f7);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;">Innovayse</span>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                          <!-- Card -->
                          <tr>
                            <td style="background-color:#1a1a1f;border:1px solid rgba(255,255,255,0.06);border-radius:16px;padding:40px 36px;">
                              <!-- Icon -->
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td align="center" style="padding-bottom:24px;">
                                    <div style="width:56px;height:56px;border-radius:50%;background:rgba(14,165,233,0.1);border:1px solid rgba(14,165,233,0.2);display:inline-flex;align-items:center;justify-content:center;">
                                      <svg width="28" height="28" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="url(#lg)" stroke-width="1.5" fill="none"/>
                                        <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="url(#lg)" opacity="0.7"/>
                                        <defs><linearGradient id="lg" x1="2" y1="2" x2="20" y2="20"><stop offset="0%" stop-color="#0ea5e9"/><stop offset="100%" stop-color="#a855f7"/></linearGradient></defs>
                                      </svg>
                                    </div>
                                  </td>
                                </tr>
                              </table>
                              <!-- Heading -->
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td align="center" style="padding-bottom:12px;">
                                    <h1 style="margin:0;font-size:22px;font-weight:700;color:#f0f0f5;letter-spacing:-0.02em;">Verify Your Email</h1>
                                  </td>
                                </tr>
                                <tr>
                                  <td align="center" style="padding-bottom:28px;">
                                    <p style="margin:0;font-size:14px;color:#8a8a9a;line-height:1.6;">Click the button below to verify your email address and activate your Innovayse admin account.</p>
                                  </td>
                                </tr>
                              </table>
                              <!-- Button -->
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td align="center" style="padding-bottom:28px;">
                                    <a href="{{ verification_link }}" style="display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#0ea5e9,#0284c7);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:10px;box-shadow:0 4px 20px rgba(14,165,233,0.25);">Verify Email Address</a>
                                  </td>
                                </tr>
                              </table>
                              <!-- Divider -->
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="border-top:1px solid rgba(255,255,255,0.06);padding-top:20px;">
                                    <p style="margin:0;font-size:12px;color:#5a5a6a;line-height:1.6;">If the button doesn't work, copy and paste this link into your browser:</p>
                                    <p style="margin:8px 0 0;font-size:12px;color:#0ea5e9;word-break:break-all;">{{ verification_link }}</p>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                          <!-- Footer -->
                          <tr>
                            <td align="center" style="padding-top:24px;">
                              <p style="margin:0;font-size:11px;color:#4a4a5a;">If you did not create this account, you can safely ignore this email.</p>
                              <p style="margin:8px 0 0;font-size:11px;color:#3a3a4a;">© Innovayse. All rights reserved.</p>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
            var template = Domain.Notifications.EmailTemplate.Create(
                "admin-email-verification",
                "Verify your email — Innovayse Admin",
                emailBody,
                "Sent to new admin users to verify their email address.");
            templateRepo.Add(template);
            await uow.SaveChangesAsync(ct);
        }

        // Generate email confirmation token and send verification email
        var confirmationToken = await userService.GenerateEmailConfirmationTokenAsync(userId, ct);
        var adminBaseUrl = configuration["AdminBaseUrl"] ?? "http://localhost:5173";
        var verificationLink = $"{adminBaseUrl}/verify-email?token={Uri.EscapeDataString(confirmationToken)}&email={Uri.EscapeDataString(request.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            request.Email,
            "admin-email-verification",
            new { verification_link = verificationLink }), ct);

        // Issue auth tokens
        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, request.Email, Roles.Admin);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

        await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        SetRefreshTokenCookie(refreshToken);
        return Ok(new AuthResultDto(accessToken, expiresAt, Roles.Admin));
    }

    /// <summary>
    /// Verifies an admin's email address using a confirmation token.
    /// </summary>
    /// <param name="token">The email confirmation token.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    [HttpGet("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmailAsync(
        [FromQuery] string token,
        [FromQuery] string email,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            return BadRequest(new { error = "Token and email are required." });
        }

        var success = await userService.ConfirmEmailAsync(email, token, ct);
        if (!success)
        {
            return BadRequest(new { error = "Invalid or expired verification token." });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Returns whether the current user's email has been verified.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object with <c>verified</c> boolean.</returns>
    [HttpGet("email-verified")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EmailVerifiedAsync(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var verified = await userService.IsEmailConfirmedAsync(userId, ct);
        return Ok(new { verified });
    }

    /// <summary>
    /// Resets a user's password using a token received via email.
    /// </summary>
    /// <param name="request">Email, token, and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] Requests.ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { error = "Email, token, and new password are required." });
        }

        var success = await userService.ResetPasswordWithTokenAsync(request.Email, request.Token, request.NewPassword, ct);
        if (!success)
        {
            return BadRequest(new { error = "Invalid or expired reset token." });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Accepts an invitation, creates the user account with the provided password,
    /// and returns an auth token for immediate login.
    /// </summary>
    /// <param name="request">Invitation token and chosen password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token, expiry, and role for the newly created user.</returns>
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResultDto>> AcceptInviteAsync([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        string userId;
        try
        {
            userId = await bus.InvokeAsync<string>(new AcceptInvitationCommand(request.Token, request.Password), ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // Generate JWT for the newly created user (same pattern as SetupAsync)
        var tokenService = HttpContext.RequestServices.GetRequiredService<ITokenService>();
        var refreshTokenRepo = HttpContext.RequestServices.GetRequiredService<IRefreshTokenRepository>();
        var uow = HttpContext.RequestServices.GetRequiredService<Application.Common.IUnitOfWork>();

        var user = await userService.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found after invitation acceptance.");

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, user.Email, Roles.Client);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

        await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        SetRefreshTokenCookie(refreshToken);
        return Ok(new AuthResultDto(accessToken, expiresAt, Roles.Client));
    }

    /// <summary>
    /// Sets the refresh token as a secure, httpOnly, SameSite=Strict cookie.
    /// The browser sends it automatically — the client JS never reads it.
    /// </summary>
    /// <param name="token">The raw refresh token string.</param>
    private void SetRefreshTokenCookie(string token)
    {
        var isDev = HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
