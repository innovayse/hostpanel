namespace Innovayse.API.Admin;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Admin.Queries.GetClientGrowthReport;
using Innovayse.Application.Admin.Queries.GetRevenueReport;
using Innovayse.Application.Admin.Queries.GetServiceUsageReport;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Provides admin reporting endpoints for revenue, client growth, and service usage.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ReportsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns daily revenue totals for the given date range.</summary>
    /// <param name="startDate">Range start (UTC).</param>
    /// <param name="endDate">Range end (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of daily revenue items.</returns>
    [HttpGet("revenue")]
    public async Task<ActionResult<IReadOnlyList<RevenueReportItemDto>>> GetRevenueAsync(
        [FromQuery] DateTimeOffset startDate,
        [FromQuery] DateTimeOffset endDate,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<RevenueReportItemDto>>(
            new GetRevenueReportQuery(startDate, endDate), ct);
        return Ok(result);
    }

    /// <summary>Returns daily new client registration counts for the given date range.</summary>
    /// <param name="startDate">Range start (UTC).</param>
    /// <param name="endDate">Range end (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of daily client growth items.</returns>
    [HttpGet("client-growth")]
    public async Task<ActionResult<IReadOnlyList<ClientGrowthReportItemDto>>> GetClientGrowthAsync(
        [FromQuery] DateTimeOffset startDate,
        [FromQuery] DateTimeOffset endDate,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientGrowthReportItemDto>>(
            new GetClientGrowthReportQuery(startDate, endDate), ct);
        return Ok(result);
    }

    /// <summary>Returns service usage counts grouped by product name.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product usage items ordered by count descending.</returns>
    [HttpGet("service-usage")]
    public async Task<ActionResult<IReadOnlyList<ServiceUsageReportItemDto>>> GetServiceUsageAsync(
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ServiceUsageReportItemDto>>(
            new GetServiceUsageReportQuery(), ct);
        return Ok(result);
    }
}
