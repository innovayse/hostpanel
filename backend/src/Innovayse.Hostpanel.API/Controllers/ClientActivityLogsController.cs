using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Audit.Queries.ListClientActivityLogs;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for viewing the per-client activity log.</summary>
[Route("api/clients/{clientId:int}/activity-logs")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientActivityLogsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientActivityLogsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated, filtered list of activity log entries for a specific client.</summary>
    /// <param name="clientId">FK to the client; wins over any client id in the query.</param>
    /// <param name="query">The page, page size, search and period to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page of activity log entries; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ActivityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(
        int clientId, [FromQuery] ListClientActivityLogsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            query with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }
}
