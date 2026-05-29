namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Support.Commands.CreateNetworkIssue;
using Innovayse.Application.Support.Commands.DeleteNetworkIssue;
using Innovayse.Application.Support.Commands.UpdateNetworkIssue;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetNetworkIssue;
using Innovayse.Application.Support.Queries.ListNetworkIssues;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing network issues and maintenance events.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/network-issues")]
[Authorize(Roles = Roles.Admin)]
public sealed class NetworkIssuesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated, optionally filtered list of network issues.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="status">Optional status filter (Reported, Investigating, Scheduled, Resolved).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated network issue list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NetworkIssueDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<NetworkIssueDto>>(
            new ListNetworkIssuesQuery(page, pageSize, status), ct);
        return Ok(result);
    }

    /// <summary>Returns a single network issue by ID.</summary>
    /// <param name="id">Network issue primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full network issue DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<NetworkIssueDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<NetworkIssueDto>(new GetNetworkIssueQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new network issue.</summary>
    /// <param name="request">Network issue creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created network issue ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateNetworkIssueRequest request, CancellationToken ct)
    {
        var cmd = new CreateNetworkIssueCommand(
            request.Title, request.Type, request.Server,
            request.Priority, request.StartDate, request.Description);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing network issue.</summary>
    /// <param name="id">Network issue primary key.</param>
    /// <param name="request">Updated network issue fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateNetworkIssueRequest request, CancellationToken ct)
    {
        var cmd = new UpdateNetworkIssueCommand(
            id, request.Title, request.Type, request.Server,
            request.Priority, request.Status, request.StartDate, request.EndDate, request.Description);
        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>Permanently deletes a network issue.</summary>
    /// <param name="id">Network issue primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteNetworkIssueCommand(id), ct);
        return NoContent();
    }
}
