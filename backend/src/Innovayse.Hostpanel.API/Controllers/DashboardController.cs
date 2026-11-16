using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardCharts;
using Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardStats;
using Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin dashboard statistics and the notification feed.</summary>
[Route("api/admin/dashboard")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class DashboardController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public DashboardController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns aggregated statistics for the admin dashboard.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the statistics; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatsAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DashboardStatsDto>>(new GetDashboardStatsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the series the dashboard charts draw for a period.</summary>
    /// <param name="query">The period to cover, 30 days when omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with income per day, new orders and clients per day, invoices by status and open tickets by priority; 400 for a period that is not offered; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("charts")]
    [ProducesResponseType(typeof(DashboardChartsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChartsAsync([FromQuery] GetDashboardChartsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DashboardChartsDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the most recent noteworthy events for the notification bell.</summary>
    /// <param name="query">The maximum number of events to return, newest first.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the merged, newest-first activity events; 401 without a session; 403 for a caller who is not staff; 400 when the body, query or route fails validation; 429 when the rate limit is hit.</returns>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActivityAsync([FromQuery] GetRecentActivityQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ActivityEventDto>>>(query, ct);
        return HandleResult(outcome);
    }
}
