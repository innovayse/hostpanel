namespace Innovayse.API.Clients;

using Innovayse.API.Clients.Requests;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.AddContact;
using Innovayse.Application.Clients.Commands.AddUserToClient;
using Innovayse.Application.Clients.Commands.AdminCreateClient;
using Innovayse.Application.Clients.Commands.CreateClient;
using Innovayse.Application.Clients.Commands.InviteUserToClient;
using Innovayse.Application.Clients.Commands.RemoveContact;
using Innovayse.Application.Clients.Commands.RemoveUserFromClient;
using Innovayse.Application.Clients.Commands.TransferOwnership;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Clients.Commands.UpdateContact;
using Innovayse.Application.Clients.Commands.UpdateUserPermissions;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetClient;
using Innovayse.Application.Clients.Queries.GetClientUsers;
using Innovayse.Application.Clients.Queries.ListClients;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin/Reseller API for client account management.
/// All endpoints require the Admin or Reseller role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="userService">User service for Identity lookups.</param>
[ApiController]
[Route("api/clients")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ClientsController(IMessageBus bus, IUserService userService) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of clients with optional filters.
    /// </summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 20).</param>
    /// <param name="search">Optional name/company search term.</param>
    /// <param name="email">Optional email partial match.</param>
    /// <param name="phone">Optional phone partial match.</param>
    /// <param name="status">Optional status filter (Active, Inactive, Suspended, Closed).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of client summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? email = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<ClientListItemDto>>(
            new ListClientsQuery(page, pageSize, search, email, phone, status), ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns full details for a single client.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full client details including contacts and billing address.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ClientDto>(new GetClientQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new client record linked to an existing Identity user.
    /// </summary>
    /// <param name="request">Client creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new client's ID.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateClientCommand(request.UserId, request.Email, request.FirstName, request.LastName, request.CompanyName),
            ct);

        return CreatedAtAction(nameof(GetByIdAsync), new { id }, new { id });
    }

    /// <summary>
    /// Updates a client's profile and billing address.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Updated client data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateClientCommand(
                id, request.Email, request.FirstName, request.LastName, request.CompanyName,
                request.Phone, request.Street, request.Address2, request.City,
                request.State, request.PostCode, request.Country,
                request.Currency, request.PaymentMethod, request.BillingContact, request.AdminNotes,
                request.NotifyGeneral, request.NotifyInvoice, request.NotifySupport,
                request.NotifyProduct, request.NotifyDomain, request.NotifyAffiliate,
                request.LateFees, request.OverdueNotices, request.TaxExempt,
                request.SeparateInvoices, request.DisableCcProcessing, request.MarketingOptIn,
                request.StatusUpdate, request.AllowSso, request.Status),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Adds an additional contact to a client account.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/contacts")]
    public async Task<IActionResult> AddContactAsync(int id, [FromBody] AddContactRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new AddContactCommand(
                id, request.FirstName, request.LastName, request.CompanyName,
                request.Email, request.Phone, request.Type,
                request.Street, request.Address2, request.City, request.State, request.PostCode, request.Country,
                request.NotifyGeneral, request.NotifyInvoice, request.NotifySupport,
                request.NotifyProduct, request.NotifyDomain, request.NotifyAffiliate),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Updates an existing contact on a client account.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="request">Updated contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}/contacts/{contactId:int}")]
    public async Task<IActionResult> UpdateContactAsync(
        int id, int contactId, [FromBody] UpdateContactRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateContactCommand(
                id, contactId,
                request.FirstName, request.LastName, request.CompanyName,
                request.Email, request.Phone, request.Type,
                request.Street, request.Address2, request.City, request.State, request.PostCode, request.Country,
                request.NotifyGeneral, request.NotifyInvoice, request.NotifySupport,
                request.NotifyProduct, request.NotifyDomain, request.NotifyAffiliate),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Removes a contact from a client account.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    public async Task<IActionResult> RemoveContactAsync(int id, int contactId, CancellationToken ct)
    {
        await bus.InvokeAsync(new RemoveContactCommand(id, contactId), ct);
        return NoContent();
    }

    /// <summary>
    /// Returns the Identity user linked to a client account.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO or 404 if no user is linked.</returns>
    [HttpGet("{id:int}/user")]
    public async Task<IActionResult> GetUserAsync(int id, CancellationToken ct)
    {
        var client = await bus.InvokeAsync<ClientDto>(new GetClientQuery(id), ct);
        var user = await userService.GetUserWithAccountsAsync(client.UserId, ct);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Creates a new client account from the admin panel.
    /// Supports creating a new Identity user or linking to an existing one,
    /// with full profile, address, notification, and billing settings.
    /// </summary>
    /// <param name="request">Admin client creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new client's ID.</returns>
    [HttpPost("admin")]
    public async Task<IActionResult> AdminCreateAsync([FromBody] AdminCreateClientRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new AdminCreateClientCommand(
                request.CreateNewUser,
                request.ExistingUserId,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.CompanyName,
                request.Phone,
                request.Street,
                request.Address2,
                request.City,
                request.State,
                request.PostCode,
                request.Country,
                request.Currency,
                request.Language,
                request.PaymentMethod,
                request.BillingContact,
                request.AdminNotes,
                request.Status,
                request.NotifyGeneral,
                request.NotifyInvoice,
                request.NotifySupport,
                request.NotifyProduct,
                request.NotifyDomain,
                request.NotifyAffiliate,
                request.LateFees,
                request.OverdueNotices,
                request.TaxExempt,
                request.SeparateInvoices,
                request.DisableCcProcessing,
                request.MarketingOptIn,
                request.StatusUpdate,
                request.AllowSso),
            ct);

        return Created($"/api/clients/{id}", new { id });
    }

    /// <summary>
    /// Lists all users linked to a client (owner + additional non-owner users).
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of users with their permissions.</returns>
    [HttpGet("{id:int}/users")]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetClientUsersAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientUserDto>>(new GetClientUsersQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Adds an additional user to a client with specified permissions.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Request body with user ID and permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/users")]
    public async Task<IActionResult> AddUserToClientAsync(
        int id, [FromBody] AddUserToClientRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new AddUserToClientCommand(id, request.UserId, request.Permissions), ct);
        return NoContent();
    }

    /// <summary>
    /// Updates permissions for a non-owner user linked to a client.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="userId">The Identity user ID whose permissions to update.</param>
    /// <param name="request">Request body with updated permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}/users/{userId}/permissions")]
    public async Task<IActionResult> UpdateUserPermissionsAsync(
        int id, string userId, [FromBody] UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateUserPermissionsCommand(id, userId, request.Permissions), ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a non-owner user from a client.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="userId">The Identity user ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}/users/{userId}")]
    public async Task<IActionResult> RemoveUserFromClientAsync(int id, string userId, CancellationToken ct)
    {
        await bus.InvokeAsync(new RemoveUserFromClientCommand(id, userId), ct);
        return NoContent();
    }

    /// <summary>
    /// Transfers account ownership to a different user.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="userId">The Identity user ID of the new owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/users/{userId}/make-owner")]
    public async Task<IActionResult> TransferOwnershipAsync(int id, string userId, CancellationToken ct)
    {
        await bus.InvokeAsync(new TransferOwnershipCommand(id, userId), ct);
        return NoContent();
    }

    /// <summary>
    /// Invites a user to a client account by email.
    /// Creates an invitation, seeds the email template on first use, and sends the invite email.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Invitation details including email, name, and permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/users/invite")]
    public async Task<IActionResult> InviteUserAsync(int id, [FromBody] InviteUserRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new InviteUserToClientCommand(id, request.Email, request.FirstName, request.LastName, request.Permissions), ct);
        return NoContent();
    }
}
