using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.AddContact;
using Innovayse.Hostpanel.Application.Clients.Commands.AddUserToClient;
using Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;
using Innovayse.Hostpanel.Application.Clients.Commands.ExportClientData;
using Innovayse.Hostpanel.Application.Clients.Commands.InviteUserToClient;
using Innovayse.Hostpanel.Application.Clients.Commands.RemoveContact;
using Innovayse.Hostpanel.Application.Clients.Commands.RemoveUserFromClient;
using Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;
using Innovayse.Hostpanel.Application.Clients.Commands.TransferOwnership;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateContact;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateUserPermissions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClient;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientSummary;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientUser;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientUsers;
using Innovayse.Hostpanel.Application.Clients.Queries.ListClients;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin API for client account management.</summary>
[Route("api/clients")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of clients with optional filters.</summary>
    /// <param name="query">The page, filters and page size to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PaginatedList<ClientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListClientsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ClientListItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns full details for a single client.</summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the client; 404 with <c>CLIENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientDto>>(new GetClientQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Replaces a client's profile, billing address, preferences, notification and billing settings.</summary>
    /// <remarks>
    /// A full replacement (PUT), not a patch: send the whole record as <c>GET /api/clients/{id}</c>
    /// returns it. Every notification and billing flag the body leaves out is stored as
    /// <c>false</c>, and every optional text field it leaves out is cleared. Only <c>email</c>,
    /// <c>language</c>, <c>currency</c> and <c>status</c> are left as they are when omitted.
    /// A save that fails because another writer changed the client first answers 409 with
    /// <c>CONFLICT</c>; read the client again and resubmit.
    /// </remarks>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="command">The complete client record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>CLIENT_NOT_FOUND</c> or <c>CURRENCY_NOT_FOUND</c>; 409 with <c>CLIENT_CURRENCY_ALREADY_INVOICED</c>, <c>EMAIL_ALREADY_IN_USE</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff, or with <c>DEMO_ACTION_DISABLED</c> for a published demo account;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateClientCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Sets or clears the link between a client and an account in another system. Staff only; the
    /// client's own profile update cannot change it.
    /// </summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="command">The provider and account id, both null to clear the link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>CLIENT_NOT_FOUND</c>; 409 with <c>EXTERNAL_ACCOUNT_ALREADY_LINKED</c>; 400 when the body or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/external-account")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetExternalAccountAsync(int id, [FromBody] SetClientExternalAccountCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an additional contact to a client account.</summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="command">The contact to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new contact's id; 404 with <c>CLIENT_NOT_FOUND</c>; 409 with <c>CONTACT_EMAIL_IN_USE</c> when another contact of the client has the address, <c>CLIENT_RECORD_CLOSED</c> when the account is closed, or <c>CONFLICT</c> when another writer changed the client first; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/contacts")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddContactAsync(int id, [FromBody] AddContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command with { ClientId = id }, ct);
        return HandleResult(outcome, contactId => CreatedResult($"/api/clients/{id}/contacts/{contactId}", contactId));
    }

    /// <summary>Updates an existing contact on a client account.</summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="contactId">The contact primary key; wins over any contact id in the body.</param>
    /// <param name="command">The updated contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>CLIENT_NOT_FOUND</c> or <c>CLIENT_CONTACT_NOT_FOUND</c>; 409 with <c>CONTACT_EMAIL_IN_USE</c>, <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateContactAsync(
        int id, int contactId, [FromBody] UpdateContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id, ContactId = contactId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a contact from a client account.</summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 404 with <c>CLIENT_NOT_FOUND</c> or <c>CLIENT_CONTACT_NOT_FOUND</c>; 409 with <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveContactAsync(int id, int contactId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RemoveContactCommand(id, contactId), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the person linked to a client account.</summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the linked user's detail; 404 with <c>IDENTITY_USER_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/user")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<UserDetailDto>>(new GetClientUserQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Creates a new client account from the admin panel. Supports creating a new Identity user or
    /// linking to an existing one, with full profile, address, notification and billing settings.
    /// </summary>
    /// <param name="command">Admin client creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new client's id; 404 with <c>CURRENCY_NOT_FOUND</c> or <c>IDENTITY_USER_NOT_FOUND</c>; 409 with <c>EXISTING_USER_ALREADY_HAS_CLIENT</c>, <c>ACCOUNT_EMAIL_EXISTS</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation, or with <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c>; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("admin")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdminCreateAsync([FromBody] AdminCreateClientCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/clients/{id}", id));
    }

    /// <summary>Lists all users linked to a client (owner + additional non-owner users).</summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the linked users; 404 with <c>CLIENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/users")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ClientUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientUsersAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientUserDto>>>(new GetClientUsersQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an additional user to a client with specified permissions.</summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="command">The user to add and the permissions to grant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when linked; 404 with <c>CLIENT_NOT_FOUND</c> or <c>IDENTITY_USER_NOT_FOUND</c> (also for a staff account, which is never linked); 409 with <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation, or with <c>CLIENT_USER_IS_OWNER</c> or <c>CLIENT_USER_LINK_DUPLICATE</c>; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/users")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUserToClientAsync(
        int id, [FromBody] AddUserToClientCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Updates permissions for a non-owner user linked to a client.</summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="userId">
    /// The Identity user id whose permissions to update; wins over any user id in the body.
    /// </param>
    /// <param name="command">The permissions to grant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>CLIENT_NOT_FOUND</c> or <c>CLIENT_USER_NOT_LINKED</c>; 409 with <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/users/{userId}/permissions")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUserPermissionsAsync(
        int id, string userId, [FromBody] UpdateUserPermissionsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id, MemberUserId = userId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a non-owner user from a client.</summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="userId">The Identity user id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 404 with <c>CLIENT_NOT_FOUND</c> or <c>CLIENT_USER_NOT_LINKED</c>; 409 with <c>CLIENT_OWNER_CANNOT_BE_REMOVED</c> for the account owner (transfer ownership first), <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}/users/{userId}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveUserFromClientAsync(int id, string userId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RemoveUserFromClientCommand(id, userId), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Transfers account ownership to a different user. The previous owner stays on the account as
    /// an additional user with every permission; removing them is a separate step.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="userId">The Identity user id of the new owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when transferred; 404 with <c>CLIENT_NOT_FOUND</c> or <c>IDENTITY_USER_NOT_FOUND</c> (the new owner is no account, or a staff account); 409 with <c>EXISTING_USER_ALREADY_HAS_CLIENT</c>, <c>CLIENT_RECORD_CLOSED</c> or <c>CONFLICT</c>; 400 when the body, query or route fails validation, or with <c>CLIENT_TRANSFER_SAME_OWNER</c>; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/users/{userId}/make-owner")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransferOwnershipAsync(int id, string userId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new TransferOwnershipCommand(id, userId), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Invites a user to a client account by email. Creates an invitation, seeds the email
    /// template on first use, and sends the invite email.
    /// </summary>
    /// <param name="id">The client primary key; wins over any client id in the body.</param>
    /// <param name="command">The invitation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when sent; 404 with <c>ACCOUNT_OWNER_NOT_FOUND</c> or <c>CLIENT_NOT_FOUND</c>; 409 with <c>INVITATION_ALREADY_PENDING</c> or <c>USER_ALREADY_LINKED</c>; 400 when the body, query or route fails validation, or with <c>CANNOT_INVITE_ACCOUNT_OWNER</c>; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/users/invite")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteUserAsync(
        int id, [FromBody] InviteUserToClientCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Generates a JSON data export for a client, limited to the sections the caller asked for.
    /// Every export is recorded in the client's activity log.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="fields">
    /// Section keys to include, e.g. <c>profileData</c>, <c>invoices</c>, <c>activityLog</c>. A
    /// section that is not named is left out of the payload.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the requested sections; 404 with <c>CLIENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/export")]
    [Authorize(Policy = Policies.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(ClientExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportClientDataAsync(int id, [FromQuery] string[] fields, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientExportDto>>(new ExportClientDataCommand(id, fields), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns aggregated summary data for a client profile dashboard.</summary>
    /// <param name="id">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the summary; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/summary")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ClientSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummaryAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientSummaryDto>>(new GetClientSummaryQuery(id), ct);
        return HandleResult(outcome);
    }
}
