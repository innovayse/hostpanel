using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Application.Support.Commands.CreateTicket;
using Innovayse.Hostpanel.Application.Support.Commands.ReplyToMyTicket;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Queries.GetMyTicket;
using Innovayse.Hostpanel.Application.Support.Queries.GetMyTickets;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client self-service endpoints for managing their own support tickets.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim: each handler resolves the caller from
/// the credential itself, so the scoping cannot come adrift from the message.
/// </remarks>
[Route("api/me/tickets")]
[Authorize(Policy = Policies.Client)]
[RequiresClientPermission(ClientPermission.ViewOpenSupportTickets)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyTicketsController : BaseApiController
{
    /// <summary>Wolverine message bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine message bus every action dispatches through.</param>
    public MyTicketsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns all tickets belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the client's ticket summaries; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the
    /// user lacks <c>ViewOpenSupportTickets</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the
    /// caller has no client record;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MyTicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<MyTicketListItemDto>>>(new GetMyTicketsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single ticket for the authenticated client.</summary>
    /// <param name="id">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the ticket; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewOpenSupportTickets</c>; 404 with
    /// <c>TICKET_NOT_FOUND</c>. A ticket belonging to another client and a ticket that does not exist
    /// answer alike: ids here are sequential, and telling those two apart is itself a way of
    /// enumerating them;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MyTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyTicketDto>>(new GetMyTicketQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new support ticket for the authenticated client.</summary>
    /// <param name="command">Ticket creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new ticket's id; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session; 403 for a caller without client access
    /// with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewOpenSupportTickets</c>; 404 with
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTicketCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/me/tickets/{id}", id));
    }

    /// <summary>Adds a client reply to an existing ticket.</summary>
    /// <param name="id">Ticket primary key; wins over any id in the body.</param>
    /// <param name="command">Reply body and author name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewOpenSupportTickets</c>; 404 with
    /// <c>TICKET_NOT_FOUND</c>. A ticket belonging to another client and a ticket that does not exist
    /// answer alike, for the same reason as the read above;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/reply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplyAsync(int id, [FromBody] ReplyToMyTicketCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { TicketId = id }, ct);
        return HandleResult(outcome);
    }
}
