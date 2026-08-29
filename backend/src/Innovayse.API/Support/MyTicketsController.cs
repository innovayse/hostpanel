namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Support.Commands.CreateTicket;
using Innovayse.Application.Support.Commands.ReplyToMyTicket;
using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.Queries.GetMyTicket;
using Innovayse.Application.Support.Queries.GetMyTickets;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client self-service endpoints for managing their own support tickets.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim: each handler resolves the caller
/// from the credential itself, so the scoping cannot come adrift from the message.
/// </remarks>
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
        var result = await bus.InvokeAsync<IReadOnlyList<TicketListItemDto>>(
            new GetMyTicketsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single ticket for the authenticated client.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Full ticket DTO. A ticket belonging to another client and a ticket that does not exist both
    /// answer 404 with <c>TICKET_NOT_FOUND</c>: ids here are sequential, and telling those two
    /// apart is itself a way of enumerating them.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<TicketDto>(new GetMyTicketQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new support ticket for the authenticated client.</summary>
    /// <param name="request">Ticket creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new ticket ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateTicketCommand(request.Subject, request.Message, request.DepartmentId, request.Priority),
            ct);
        return StatusCode(201, id);
    }

    /// <summary>Adds a client reply to an existing ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="request">Reply body and author name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 No Content on success. A ticket belonging to another client and a ticket that does not
    /// exist both answer 404 with <c>TICKET_NOT_FOUND</c>, for the same reason as the read above.
    /// </returns>
    [HttpPost("{id:int}/reply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplyAsync(int id, [FromBody] ReplyToTicketRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new ReplyToMyTicketCommand(id, request.Message, request.AuthorName),
            ct);
        return NoContent();
    }
}
