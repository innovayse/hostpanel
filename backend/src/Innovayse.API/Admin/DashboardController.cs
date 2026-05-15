namespace Innovayse.API.Admin;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Admin.Queries.GetDashboardStats;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Provides admin dashboard statistics endpoints.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class DashboardController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns aggregated statistics for the admin dashboard.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dashboard statistics DTO.</returns>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStatsAsync(CancellationToken ct)
    {
        var stats = await bus.InvokeAsync<DashboardStatsDto>(new GetDashboardStatsQuery(), ct);
        return Ok(stats);
    }
}
