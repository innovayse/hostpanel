namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing Identity users.
/// Requires the Admin role.
/// </summary>
/// <param name="userService">User management service.</param>
/// <param name="bus">Wolverine message bus for sending emails.</param>
/// <param name="configuration">App configuration for reading base URLs.</param>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminUsersController(
    IUserService userService,
    IMessageBus bus,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Returns a paginated list of all users.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 20).</param>
    /// <param name="search">Optional search term (name or email).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of user summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var ps = Math.Clamp(pageSize, 1, 100);
        var pg = Math.Max(1, page);
        var (items, totalCount) = await userService.ListUsersAsync(pg, ps, search, ct);
        return Ok(new PagedResult<UserListItemDto>(items, totalCount, pg, ps));
    }

    /// <summary>Returns a single user with linked client accounts.</summary>
    /// <param name="id">Identity user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailDto>> GetByIdAsync(string id, CancellationToken ct)
    {
        var dto = await userService.GetUserWithAccountsAsync(id, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    /// <summary>Updates a user's profile fields.</summary>
    /// <param name="id">Identity user ID.</param>
    /// <param name="request">Updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        await userService.UpdateUserAsync(id, request.FirstName, request.LastName, request.Email, request.Language, ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes an Identity user. Client records are preserved as orphans.
    /// </summary>
    /// <param name="id">Identity user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken ct)
    {
        await userService.DeleteUserAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Generates a password reset token and sends a reset email to the user.
    /// Seeds the email template on first use.
    /// </summary>
    /// <param name="id">Identity user ID.</param>
    /// <param name="templateRepo">Email template repository.</param>
    /// <param name="uow">Unit of work for persisting the template.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync(
        string id,
        [FromServices] IEmailTemplateRepository templateRepo,
        [FromServices] IUnitOfWork uow,
        CancellationToken ct)
    {
        var user = await userService.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"User {id} not found.");

        // Seed the password reset template on first use
        const string slug = "user-password-reset";
        var existing = await templateRepo.FindBySlugAsync(slug, ct);
        if (existing is null)
        {
            var body = """
                <!DOCTYPE html>
                <html>
                <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
                <body style="margin:0;padding:0;background-color:#0a0a0f;font-family:'Inter',system-ui,-apple-system,sans-serif;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0f;padding:40px 20px;">
                    <tr><td align="center">
                      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;">
                        <tr><td align="center" style="padding-bottom:32px;">
                          <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                            <td style="background:linear-gradient(135deg,rgba(14,165,233,0.1),rgba(168,85,247,0.1));border:1px solid rgba(14,165,233,0.2);border-radius:10px;padding:8px 16px;">
                              <span style="font-size:16px;font-weight:700;background:linear-gradient(135deg,#0ea5e9,#a855f7);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;">Innovayse</span>
                            </td>
                          </tr></table>
                        </td></tr>
                        <tr><td style="background-color:#1a1a1f;border:1px solid rgba(255,255,255,0.06);border-radius:16px;padding:40px 36px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr><td align="center" style="padding-bottom:24px;">
                              <h1 style="margin:0;font-size:22px;font-weight:700;color:#f0f0f5;">Reset Your Password</h1>
                            </td></tr>
                            <tr><td align="center" style="padding-bottom:28px;">
                              <p style="margin:0;font-size:14px;color:#8a8a9a;line-height:1.6;">Click the button below to reset your password. This link will expire in 24 hours.</p>
                            </td></tr>
                            <tr><td align="center" style="padding-bottom:28px;">
                              <a href="{{ reset_link }}" style="display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#0ea5e9,#0284c7);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:10px;box-shadow:0 4px 20px rgba(14,165,233,0.25);">Reset Password</a>
                            </td></tr>
                            <tr><td style="border-top:1px solid rgba(255,255,255,0.06);padding-top:20px;">
                              <p style="margin:0;font-size:12px;color:#5a5a6a;line-height:1.6;">If you didn't request this, you can safely ignore this email.</p>
                              <p style="margin:8px 0 0;font-size:12px;color:#0ea5e9;word-break:break-all;">{{ reset_link }}</p>
                            </td></tr>
                          </table>
                        </td></tr>
                        <tr><td align="center" style="padding-top:24px;">
                          <p style="margin:0;font-size:11px;color:#3a3a4a;">© Innovayse. All rights reserved.</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;
            var template = EmailTemplate.Create(slug, "Reset your password — Innovayse", body,
                "Sent when an admin initiates a password reset for a user.");
            templateRepo.Add(template);
            await uow.SaveChangesAsync(ct);
        }

        var token = await userService.GeneratePasswordResetTokenAsync(id, ct);
        var clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{clientBaseUrl}/client/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            user.Email,
            slug,
            new { reset_link = resetLink }), ct);

        return NoContent();
    }

    /// <summary>
    /// Sets a new password for a user (admin action).
    /// </summary>
    /// <param name="id">Identity user ID.</param>
    /// <param name="request">New password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(string id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await userService.ChangePasswordAsync(id, request.Password, ct);
        return NoContent();
    }
}
