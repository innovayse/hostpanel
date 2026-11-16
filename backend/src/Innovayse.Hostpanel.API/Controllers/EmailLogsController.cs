using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Queries.ListClientEmailLogs;
using Innovayse.Hostpanel.Application.Notifications.Queries.ListEmailLogs;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for the platform's outbound email delivery log.</summary>
[Route("api/admin/email-logs")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class EmailLogsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public EmailLogsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a page of email log entries, narrowed by the search and filters, newest first.</summary>
    /// <param name="query">The page, page size, search, outcome and period to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page; 401 without a session; 403 for a caller who is not an admin; 400 when the body, query or route fails validation; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<EmailLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListEmailLogsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<EmailLogDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a page of email logs sent to a specific client.</summary>
    /// <param name="clientId">Client primary key; wins over any id in the query string.</param>
    /// <param name="query">The page, page size, search, outcome and period to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page, empty when the client id names nobody; 401 without a session; 403 for a caller who is not an admin; 400 when the body, query or route fails validation; 429 when the rate limit is hit.</returns>
    [HttpGet("client/{clientId:int}")]
    [ProducesResponseType(typeof(PaginatedList<EmailLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListByClientAsync(
        int clientId, [FromQuery] ListClientEmailLogsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<EmailLogDto>>>(query with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }
}
