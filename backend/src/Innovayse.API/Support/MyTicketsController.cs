namespace Innovayse.API.Support;

using System.Security.Claims;
using Innovayse.API.Support.Requests;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Application.Support.Commands.CreateTicket;
using Innovayse.Application.Support.Commands.ReplyToTicket;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetMyTickets;
using Innovayse.Application.Support.Queries.GetTicket;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client self-service endpoints for managing their own support tickets.
/// The client ID is resolved from the JWT via <see cref="GetMyProfileQuery"/>.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me/tickets")]
[Authorize(Roles = Roles.Client)]
public sealed class MyTicketsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all tickets belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of the client's ticket summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> GetMyTicketsAsync(CancellationToken ct)
    {
        var clientId = await GetClientIdAsync(ct);
        var result = await bus.InvokeAsync<IReadOnlyList<TicketListItemDto>>(
            new GetMyTicketsQuery(clientId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single ticket for the authenticated client.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full ticket DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<TicketDto>(new GetTicketQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new support ticket for the authenticated client.</summary>
    /// <param name="request">Ticket creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new ticket ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var clientId = await GetClientIdAsync(ct);
        var id = await bus.InvokeAsync<int>(
            new CreateTicketCommand(clientId, request.Subject, request.Message, request.DepartmentId, request.Priority),
            ct);
        return StatusCode(201, id);
    }

    /// <summary>Adds a client reply to an existing ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="request">Reply body and author name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> ReplyAsync(int id, [FromBody] ReplyToTicketRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new ReplyToTicketCommand(id, request.Message, request.AuthorName, false),
            ct);
        return NoContent();
    }

    /// <summary>Extracts the authenticated user's Identity ID from JWT claims.</summary>
    /// <returns>The user ID string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user ID claim is missing.</exception>
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

    /// <summary>
    /// Resolves the numeric client ID by loading the client profile via the Wolverine bus.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The numeric client ID.</returns>
    private async Task<int> GetClientIdAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        return profile.Id;
    }
}
