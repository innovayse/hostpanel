using Innovayse.Hostpanel.Application.StatusPage.Commands.ConfigureStatusComponentMonitoring;
using Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponent;
using Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponentGroup;
using Innovayse.Hostpanel.Application.StatusPage.Commands.OpenStatusIncident;
using Innovayse.Hostpanel.Application.StatusPage.Commands.PostStatusIncidentUpdate;
using Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentOverride;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusOverview;
using Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusUptime;
using Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusIncidents;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The public status page: the anonymous feeds visitors read, and the admin panel's management of what they show.</summary>
[Route("api/admin/status")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class StatusPageController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public StatusPageController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Creates a group that components are listed under.</summary>
    /// <param name="command">The group's name and position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new group's id; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("groups")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateGroupAsync([FromBody] CreateStatusComponentGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/status/groups/{id}", id));
    }

    /// <summary>Adds an operational component to a group.</summary>
    /// <param name="command">The group, and the component's name, description and position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new component's id; 400 when a value is refused; 404 when the group id names no group;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("components")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateComponentAsync([FromBody] CreateStatusComponentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/status/components/{id}", id));
    }

    /// <summary>
    /// Forces a state onto the status page for a component whatever its automatic checks find, or, with no state,
    /// hands the state back to the checks.
    /// </summary>
    /// <param name="id">The component identifier; wins over any id in the body.</param>
    /// <param name="command">The state to force, or <c>null</c> to clear the override.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no component;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("components/{id:int}/override")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetComponentOverrideAsync(int id, [FromBody] SetStatusComponentOverrideCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Sets how a component is monitored automatically: the address to check, whether it runs and the failure threshold.</summary>
    /// <param name="id">The component identifier; wins over any id in the body.</param>
    /// <param name="command">The monitoring settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no component;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("components/{id:int}/monitoring")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConfigureComponentMonitoringAsync(int id, [FromBody] ConfigureStatusComponentMonitoringCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Announces an incident.</summary>
    /// <param name="command">The incident's title and first message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new incident's id; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("incidents")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenIncidentAsync([FromBody] OpenStatusIncidentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/status/incidents/{id}", id));
    }

    /// <summary>Adds a timeline entry to an incident and moves it to a state; resolving closes it.</summary>
    /// <param name="id">The incident identifier; wins over any id in the body.</param>
    /// <param name="command">The state the incident moves to and the message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no incident; 409 when the incident is already resolved;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("incidents/{id:int}/updates")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostIncidentUpdateAsync(int id, [FromBody] PostStatusIncidentUpdateCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// The public status page: every component with its state, the overall state and the open incidents.
    /// Anonymous: visitors need it exactly when they cannot sign in.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the overview; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/status")]
    [AllowAnonymous]
    [PublicContentCache]
    [ProducesResponseType(typeof(StatusOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<StatusOverviewDto>>(new GetStatusOverviewQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>The incidents announced within the last days, open and resolved, with their timelines. Anonymous.</summary>
    /// <param name="query">How many days back to look; 90 when omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the incidents, newest first; 400 when the window is refused; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/status/incidents")]
    [AllowAnonymous]
    [PublicContentCache]
    [ProducesResponseType(typeof(IReadOnlyList<StatusIncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListIncidentsAsync([FromQuery] ListStatusIncidentsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<StatusIncidentDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>The uptime of every component over the last 30, 60 or 90 days, with a daily series for the uptime bars. Anonymous.</summary>
    /// <param name="query">The window in days: 30, 60 or 90; 90 when omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the uptime figures; 400 when the window is refused; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/status/uptime")]
    [AllowAnonymous]
    [PublicContentCache]
    [ProducesResponseType(typeof(StatusUptimeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUptimeAsync([FromQuery] GetStatusUptimeQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<StatusUptimeDto>>(query, ct);
        return HandleResult(outcome);
    }
}
