using Innovayse.Hostpanel.Application.Support.Commands.CreateNetworkIssue;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteNetworkIssue;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateNetworkIssue;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Queries.GetNetworkIssue;
using Innovayse.Hostpanel.Application.Support.Queries.ListNetworkIssues;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for managing network issues and maintenance events.
/// </summary>
[Route("api/network-issues")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class NetworkIssuesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public NetworkIssuesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated, searchable list of network issues, narrowed by status, type and priority.</summary>
    /// <param name="query">The page, page size, search, status, type and priority to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 with <c>VALIDATION_ERROR</c> for a bad paging window or filter; 401 without a session
    /// without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<NetworkIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListNetworkIssuesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<NetworkIssueDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single network issue by ID.</summary>
    /// <param name="id">Network issue primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the issue; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a
    /// session; 403 for a caller who is not an administrator; 404 with <c>NETWORKISSUE_NOT_FOUND</c>
    /// when the id names no issue;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(NetworkIssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<NetworkIssueDto>>(new GetNetworkIssueQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new network issue.</summary>
    /// <param name="command">Network issue creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new issue's id; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session
    /// without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateNetworkIssueCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/network-issues/{id}", id));
    }

    /// <summary>Updates an existing network issue.</summary>
    /// <param name="id">Network issue primary key; wins over any id in the body.</param>
    /// <param name="command">Updated network issue fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session;
    /// 403 for a caller who is not an administrator; 404 with <c>NETWORKISSUE_NOT_FOUND</c> when the id
    /// names no issue;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateNetworkIssueCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a network issue.</summary>
    /// <param name="id">Network issue primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session;
    /// 403 for a caller who is not an administrator; 404 with <c>NETWORKISSUE_NOT_FOUND</c> when the id
    /// names no issue;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteNetworkIssueCommand(id), ct);
        return HandleResult(outcome);
    }
}
