namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Notifications.Interfaces;
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
/// <param name="identity">Reads people from wherever they live.</param>
/// <param name="provisioning">Writes them, where this deployment owns them.</param>
/// <param name="clientRepo">Client repository, for the accounts each person owns.</param>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminUsersController(
    IIdentityProvider identity,
    IUserProvisioning provisioning,
    IClientRepository clientRepo) : ControllerBase
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

        var (accounts, total) = await identity.ListAsync(search, pg, ps, ct);

        // One lookup for the page rather than one per row.
        var clientIds = await clientRepo.FindClientIdsByUserIdsAsync(
            accounts.Select(a => a.Subject).ToList(), ct);

        var items = accounts.Select(a => new UserListItemDto(
            a.Subject,
            clientIds.TryGetValue(a.Subject, out var clientId) ? clientId : null,
            a.FirstName, a.LastName, a.Email,
            // Language and the account's own creation date belong to whichever store holds
            // the person, and an SSO does not hand either out. Left unanswered rather than
            // guessed at: a fabricated date on an admin screen is worse than a blank one.
            Language: null, a.LastLoginAt, CreatedAt: default)).ToList();

        return Ok(new PagedResult<UserListItemDto>(items, total, pg, ps));
    }

    /// <summary>Returns a single user with linked client accounts.</summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailDto>> GetByIdAsync(string id, CancellationToken ct)
    {
        var account = await identity.FindBySubjectAsync(id, ct);
        if (account is null)
        {
            return NotFound();
        }

        var client = await clientRepo.FindByUserIdAsync(id, ct);
        var accounts = client is not null
            ? new List<UserAccountDto>
            {
                new(client.Id, client.FirstName, client.LastName, client.CompanyName, IsOwner: true),
            }
            : [];

        return Ok(new UserDetailDto(
            account.Subject, account.FirstName, account.LastName, account.Email,
            Language: null, account.LastLoginAt,
            client?.CreatedAt ?? default, accounts));
    }

    /// <summary>Updates a user's profile fields.</summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="request">Updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        // Two writes, because they are two different things: a rename, and a change to the
        // address someone signs in with. Both refuse together where an SSO owns the person,
        // so neither can land without the other.
        await provisioning.UpdateProfileAsync(id, request.FirstName, request.LastName, request.Language, ct);
        await provisioning.ChangeEmailAsync(id, request.Email, ct);
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
        await provisioning.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sends the user a link they can use to choose a new password.
    /// Seeds the email template on first use.
    /// </summary>
    /// <param name="id">The person's subject.</param>
    /// <param name="templateRepo">Email template repository.</param>
    /// <param name="uow">Unit of work for persisting the template.</param>
    /// <param name="bus">Message bus, for sending the mail.</param>
    /// <param name="configuration">Configuration, for the client base URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync(
        string id,
        [FromServices] IEmailTemplateRepository templateRepo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IMessageBus bus,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        var account = await identity.FindBySubjectAsync(id, ct)
            ?? throw new InvalidOperationException($"User {id} not found.");

        // The token first: it is the half that refuses where an SSO owns the account, and
        // seeding a template and sending mail before finding that out would leave an
        // operator with a delivered reset link that resets nothing.
        var token = await provisioning.IssuePasswordResetTokenAsync(id, ct);

        await PasswordResetTemplateSeeder.EnsureSeededAsync(templateRepo, uow, ct);

        var clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{clientBaseUrl}/client/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(account.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            account.Email,
            PasswordResetTemplateSeeder.Slug,
            new { reset_link = resetLink }), ct);

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
        await provisioning.SetPasswordAsync(id, request.Password, ct);
        return NoContent();
    }
}
