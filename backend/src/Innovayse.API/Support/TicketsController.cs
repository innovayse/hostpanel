namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Support.Commands.AdminCreateTicket;
using Innovayse.Application.Support.Commands.AssignTicket;
using Innovayse.Application.Support.Commands.CloseTicket;
using Innovayse.Application.Support.Commands.DeleteTicket;
using Innovayse.Application.Support.Commands.ReplyToTicket;
using Innovayse.Application.Support.Commands.UpdateTicket;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Commands.AddTicketTag;
using Innovayse.Application.Support.Commands.BulkUpdateTickets;
using Innovayse.Application.Support.Commands.LeaveFeedback;
using Innovayse.Application.Support.Commands.RemoveTicketTag;
using Innovayse.Application.Support.Commands.ToggleTicketFlag;
using Innovayse.Application.Support.Queries.GetClientTicketStats;
using Innovayse.Application.Support.Queries.GetSupportOverview;
using Innovayse.Application.Support.Queries.GetTicket;
using Innovayse.Application.Support.Queries.ListClientTickets;
using Innovayse.Application.Support.Queries.ListDepartments;
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
    /// <summary>Returns a paginated, optionally filtered list of all support tickets.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="status">Optional status filter (Open, AwaitingReply, Answered, Closed, flagged).</param>
    /// <param name="search">Optional subject search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated ticket list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<TicketListItemDto>>(
            new ListTicketsQuery(page, pageSize, status, search), ct);
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

    /// <summary>Creates a support ticket on behalf of a client.</summary>
    /// <param name="request">Ticket creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created ticket ID.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> CreateAsync([FromBody] AdminCreateTicketRequest request, CancellationToken ct)
    {
        var cmd = new AdminCreateTicketCommand(
            request.ClientId, request.Subject, request.Message, request.DepartmentId, request.Priority);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates a ticket's metadata (status, priority, department, assignment).</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="request">Fields to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var cmd = new UpdateTicketCommand(id, request.Status, request.Priority, request.DepartmentId, request.AssignedToStaffId);
        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>Permanently deletes a ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteTicketCommand(id), ct);
        return NoContent();
    }

    /// <summary>Returns a paginated list of tickets for a specific client.</summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="search">Optional subject search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated ticket list.</returns>
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetByClientAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<TicketListItemDto>>(
            new ListClientTicketsQuery(clientId, page, pageSize, search), ct);
        return Ok(result);
    }

    /// <summary>Returns ticket statistics for a specific client.</summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Client ticket statistics.</returns>
    [HttpGet("client/{clientId:int}/stats")]
    public async Task<ActionResult<ClientTicketStatsDto>> GetClientStatsAsync(int clientId, CancellationToken ct)
    {
        var stats = await bus.InvokeAsync<ClientTicketStatsDto>(
            new GetClientTicketStatsQuery(clientId), ct);
        return Ok(stats);
    }

    /// <summary>Returns all support departments.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of department DTOs.</returns>
    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartmentsAsync(CancellationToken ct)
    {
        var departments = await bus.InvokeAsync<IReadOnlyList<DepartmentDto>>(
            new ListDepartmentsQuery(), ct);
        return Ok(departments);
    }

    /// <summary>Returns support overview statistics for a time period.</summary>
    /// <param name="period">Period filter: today, yesterday, last7days, last30days.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Overview statistics DTO.</returns>
    [HttpGet("overview")]
    public async Task<ActionResult<SupportOverviewDto>> GetOverviewAsync(
        [FromQuery] string period = "today",
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<SupportOverviewDto>(
            new GetSupportOverviewQuery(period), ct);
        return Ok(result);
    }

    /// <summary>Toggles the flagged state of a ticket.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/toggle-flag")]
    public async Task<IActionResult> ToggleFlagAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new ToggleTicketFlagCommand(id), ct);
        return NoContent();
    }

    /// <summary>Performs a bulk action on multiple tickets.</summary>
    /// <param name="request">Ticket IDs and action to perform.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("bulk")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> BulkActionAsync([FromBody] BulkActionRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new BulkUpdateTicketsCommand(request.TicketIds, request.Action), ct);
        return NoContent();
    }

    /// <summary>Records client feedback (rating + comment) for a ticket.</summary>
    [HttpPost("{id:int}/feedback")]
    public async Task<IActionResult> LeaveFeedbackAsync(int id, [FromBody] LeaveFeedbackRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new LeaveFeedbackCommand(id, request.Rating, request.Comment, request.LeftBy), ct);
        return NoContent();
    }

    /// <summary>Adds a tag to a ticket.</summary>
    [HttpPost("{id:int}/tags")]
    public async Task<IActionResult> AddTagAsync(int id, [FromBody] TicketTagRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new AddTicketTagCommand(id, request.Tag), ct);
        return NoContent();
    }

    /// <summary>Removes a tag from a ticket.</summary>
    [HttpDelete("{id:int}/tags/{tag}")]
    public async Task<IActionResult> RemoveTagAsync(int id, string tag, CancellationToken ct)
    {
        await bus.InvokeAsync(new RemoveTicketTagCommand(id, tag), ct);
        return NoContent();
    }
}
