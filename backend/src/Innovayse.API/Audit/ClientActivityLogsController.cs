namespace Innovayse.API.Audit;

using Innovayse.Application.Audit.DTOs;
using Innovayse.Application.Audit.Queries.ListClientActivityLogs;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for viewing the per-client activity log.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/clients/{clientId:int}/activity-logs")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ClientActivityLogsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated, filtered list of activity log entries for a specific client.</summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Entries per page (default 25).</param>
    /// <param name="date">Optional UTC date filter.</param>
    /// <param name="adminSearch">Optional partial match on admin name or email.</param>
    /// <param name="description">Optional partial match on description.</param>
    /// <param name="ipAddress">Optional partial match on IP address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged activity log entries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityLogDto>>> GetAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTimeOffset? date = null,
        [FromQuery] string? adminSearch = null,
        [FromQuery] string? description = null,
        [FromQuery] string? ipAddress = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<ActivityLogDto>>(
            new ListClientActivityLogsQuery(clientId, page, pageSize, date, adminSearch, description, ipAddress), ct);
        return Ok(result);
    }
}
