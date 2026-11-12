using Innovayse.Hostpanel.Application.StatusPage.Commands.CancelStatusMaintenance;
using Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusMaintenance;
using Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusMaintenance;
using Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusMaintenance;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Queries.ListAdminStatusMaintenance;
using Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusMaintenance;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The maintenance windows of the public status page: the anonymous feed visitors read, and the admin panel's management of them.</summary>
[Route("api/admin/status/maintenance")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class StatusMaintenanceController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public StatusMaintenanceController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Announces a maintenance window.</summary>
    /// <param name="command">The window's title, description, schedule and affected components.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new window's id; 400 when a value is refused; 404 when an affected component id names no component;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateStatusMaintenanceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/status/maintenance/{id}", id));
    }

    /// <summary>Changes a maintenance window that has not ended or been cancelled.</summary>
    /// <param name="id">The window identifier; wins over any id in the body.</param>
    /// <param name="command">The window's new title, description, schedule and affected components.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no window or an affected component id names no
    /// component; 409 when the window already ended or was cancelled; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateStatusMaintenanceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Calls off a maintenance window that has not ended.</summary>
    /// <param name="id">The window identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no window; 409 when the window already ended or was cancelled;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new CancelStatusMaintenanceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a maintenance window, whatever its state.</summary>
    /// <param name="id">The window identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no window; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteStatusMaintenanceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Every maintenance window, whatever its state, newest scheduled start first.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the windows; 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatusMaintenanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAllAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<StatusMaintenanceDto>>>(new ListAdminStatusMaintenanceQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>The maintenance windows that are under way or still to come, soonest first. Anonymous.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the windows; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/status/maintenance")]
    [AllowAnonymous]
    [PublicContentCache]
    [ProducesResponseType(typeof(IReadOnlyList<StatusMaintenanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<StatusMaintenanceDto>>>(new ListStatusMaintenanceQuery(), ct);
        return HandleResult(outcome);
    }
}
