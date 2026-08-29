namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Common;
using Innovayse.Application.Admin.Users.Commands.DeleteUser;
using Innovayse.Application.Admin.Users.Commands.SendPasswordReset;
using Innovayse.Application.Admin.Users.Commands.SetUserPassword;
using Innovayse.Application.Admin.Users.Commands.UpdateUser;
using Innovayse.Application.Admin.Users.Queries.GetUser;
using Innovayse.Application.Admin.Users.Queries.ListUsers;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing people.
/// Requires the Admin role.
/// </summary>
/// <remarks>
/// Reads work in both modes. The writes below — editing a profile, changing an address,
/// deleting an account, setting or resetting a password — are only this product's to make
/// where it owns the accounts; where an SSO owns them, they are made there. Rather than
/// answer 404 and leave an operator guessing, each returns a message naming where to go.
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminUsersController(IMessageBus bus) : ControllerBase
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
        var result = await bus.InvokeAsync<PagedResult<UserListItemDto>>(
            new ListUsersQuery(search, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a single user with linked client accounts.</summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO, or 404 if the subject is unknown.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailDto>> GetByIdAsync(string id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<UserDetailDto?>(new GetUserQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Updates a user's profile fields.</summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="request">Updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateUserCommand(id, request.FirstName, request.LastName, request.Email, request.Language), ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes an account. Client records that reference it are preserved as orphans.
    /// </summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteUserCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Sends the user a link they can use to choose a new password.
    /// Seeds the email template on first use.
    /// </summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync(string id, CancellationToken ct)
    {
        await bus.InvokeAsync(new SendPasswordResetCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Sets a new password for a user (admin action).
    /// </summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="request">New password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(string id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetUserPasswordCommand(id, request.Password), ct);
        return NoContent();
    }
}
