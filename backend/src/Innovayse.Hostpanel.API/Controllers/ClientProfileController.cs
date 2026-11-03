using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Application.Clients.Commands.AddMyContact;
using Innovayse.Hostpanel.Application.Clients.Commands.ConfirmMyEmailChange;
using Innovayse.Hostpanel.Application.Clients.Commands.RemoveMyContact;
using Innovayse.Hostpanel.Application.Clients.Commands.StartMyEmailChange;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateMyContact;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateMyProfile;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetMyContacts;
using Innovayse.Hostpanel.Application.Clients.Queries.GetMyProfile;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Client self-service portal endpoints: viewing and updating the caller's own profile.</summary>
/// <remarks>
/// Which account is never in the route or the body, and is not read from a claim here either:
/// every use case below resolves the caller inside its own handler, so each action binds and
/// dispatches exactly once.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </remarks>
[Route("api/clients/me")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientProfileController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientProfileController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the authenticated client's own profile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the profile; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [AvailableToEveryClientUser]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfileAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyProfileDto>>(new GetMyProfileQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Partially updates the authenticated client's own profile, billing address and email
    /// preferences: only the members present in the body change. The sign-in address is not one of
    /// them; it changes through <see cref="StartMyEmailChangeAsync"/>.
    /// </summary>
    /// <param name="command">The members to change. Staff-owned fields are not part of it, and a client id in the body, if any, is ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the updated profile; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client or lacks the profile permission;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPatch]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ModifyMasterProfile)]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfileAsync([FromBody] UpdateMyProfileCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyProfileDto>>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Asks to move the authenticated client's sign-in to a new address: checks the current
    /// password and mails a confirmation link to the new address. Nothing changes until the link is followed.
    /// </summary>
    /// <param name="command">The new address and the current password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once the link is on its way; 400 with <c>VALIDATION_ERROR</c> or <c>CURRENT_PASSWORD_INVALID</c>;
    /// 401 without a session; 403 for a caller who is not a client or lacks the profile permission, or
    /// <c>DEMO_ACTION_DISABLED</c> or <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c>; 404 with
    /// <c>IDENTITY_USER_NOT_FOUND</c>; 409 with <c>EMAIL_ALREADY_IN_USE</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("email-change")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ModifyMasterProfile)]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartMyEmailChangeAsync(
        [FromBody] StartMyEmailChangeCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Makes the authenticated client's address change effective with the token mailed to the new
    /// address. Every session the account held ends, this one included, and the previous address is told.
    /// </summary>
    /// <param name="command">The new address and the token from the link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once the address has changed; 400 with <c>VALIDATION_ERROR</c> or <c>TOKEN_INVALID_OR_EXPIRED</c>;
    /// 401 without a session; 403 for a caller who is not a client or lacks the profile permission, or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c>; 404 with <c>IDENTITY_USER_NOT_FOUND</c>;
    /// 409 with <c>EMAIL_ALREADY_IN_USE</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("email-change/confirm")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ModifyMasterProfile)]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmMyEmailChangeAsync(
        [FromBody] ConfirmMyEmailChangeCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the additional contacts on the authenticated client's own account.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the account's contacts; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client or lacks the contacts permission;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("contacts")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewManageContacts)]
    [ProducesResponseType(typeof(IReadOnlyList<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyContactsAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ContactDto>>>(new GetMyContactsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds a contact to the authenticated client's own account.</summary>
    /// <param name="command">The contact to add. A client id in the body, if any, is ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new contact's id; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>CONTACT_EMAIL_IN_USE</c>, <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client or lacks the contacts permission;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("contacts")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewManageContacts)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMyContactAsync([FromBody] AddMyContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, contactId => CreatedResult($"/api/clients/me/contacts/{contactId}", contactId));
    }

    /// <summary>Updates a contact on the authenticated client's own account.</summary>
    /// <param name="contactId">The contact primary key; wins over any contact id in the body.</param>
    /// <param name="command">The updated contact data. A client id in the body, if any, is ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> or <c>CLIENT_CONTACT_NOT_FOUND</c>; 409 with <c>CONTACT_EMAIL_IN_USE</c>, <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client or lacks the contacts permission;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("contacts/{contactId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewManageContacts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMyContactAsync(
        int contactId, [FromBody] UpdateMyContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ContactId = contactId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a contact from the authenticated client's own account.</summary>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> or <c>CLIENT_CONTACT_NOT_FOUND</c>; 409 with <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not a client or lacks the contacts permission;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("contacts/{contactId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewManageContacts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMyContactAsync(int contactId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RemoveMyContactCommand(contactId), ct);
        return HandleResult(outcome);
    }
}
