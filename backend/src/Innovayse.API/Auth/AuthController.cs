namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.AcceptInvitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Auth endpoints remaining after SSO migration.
/// Login/Register/Refresh/Logout are handled by the Nuxt BFF via SSO directly.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMessageBus bus, IUserService userService) : ControllerBase
{
    /// <summary>Returns whether initial admin setup is required (no users exist).</summary>
    [HttpGet("setup-required")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupRequiredAsync(CancellationToken ct)
    {
        var anyUsers = await userService.AnyUsersExistAsync(ct);
        return Ok(new { required = !anyUsers });
    }

    /// <summary>
    /// Initial setup: assigns Admin role to the first user who calls this endpoint.
    /// The caller must be authenticated via SSO (sends a valid Bearer token).
    /// Only works when no users exist yet.
    /// </summary>
    [HttpPost("setup")]
    [Authorize]
    public async Task<IActionResult> SetupAsync(CancellationToken ct)
    {
        var anyUsers = await userService.AnyUsersExistAsync(ct);
        if (anyUsers)
            return Conflict(new { error = "Setup already completed." });

        var sub = User.FindFirst("sub")?.Value;
        if (sub is null) return Unauthorized();

        var found = await userService.FindBySsoSubjectAsync(sub, ct);
        if (found is null) return Unauthorized();

        await userService.AddToRoleAsync(found.Value.Id, Innovayse.Domain.Auth.Roles.Admin, ct);
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
    /// Accepts an invitation. The invitation token is validated and the
    /// current SSO user is assigned the role defined in the invitation.
    /// The caller must be authenticated via SSO.
    /// </summary>
    [HttpPost("accept-invite")]
    [Authorize]
    public async Task<IActionResult> AcceptInviteAsync([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (sub is null) return Unauthorized();

        var found = await userService.FindBySsoSubjectAsync(sub, ct);
        if (found is null) return Unauthorized();

        try
        {
            await bus.InvokeAsync(new AcceptInvitationCommand(request.Token, found.Value.Id), ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { success = true });
    }
}
