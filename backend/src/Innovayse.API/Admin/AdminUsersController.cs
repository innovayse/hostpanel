namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Auth;
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
        if (dto is null)
        {
            return NotFound();
        }

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

        await PasswordResetTemplateSeeder.EnsureSeededAsync(templateRepo, uow, ct);

        var token = await userService.GeneratePasswordResetTokenAsync(id, ct);
        var clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{clientBaseUrl}/client/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            user.Email,
            PasswordResetTemplateSeeder.Slug,
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
