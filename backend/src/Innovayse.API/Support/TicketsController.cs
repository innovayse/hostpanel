namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Support.Commands.AssignTicket;
using Innovayse.Application.Support.Commands.CloseTicket;
using Innovayse.Application.Support.Commands.ReplyToTicket;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetTicket;
using Innovayse.Application.Support.Queries.ListTickets;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin and Reseller endpoints for managing support tickets.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/tickets")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class TicketsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all support tickets.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated ticket list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<TicketListItemDto>>(
            new ListTicketsQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a single ticket with all its replies.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full ticket DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<TicketDto>(new GetTicketQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Adds a staff reply to a ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="request">Reply body and author name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> ReplyAsync(int id, [FromBody] ReplyToTicketRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new ReplyToTicketCommand(id, request.Message, request.AuthorName, true), ct);
        return NoContent();
    }

    /// <summary>Assigns a ticket to a staff member.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="request">Staff member ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> AssignAsync(int id, [FromBody] AssignTicketRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new AssignTicketCommand(id, request.StaffId), ct);
        return NoContent();
    }

    /// <summary>Closes a ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> CloseAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new CloseTicketCommand(id), ct);
        return NoContent();
    }
}
