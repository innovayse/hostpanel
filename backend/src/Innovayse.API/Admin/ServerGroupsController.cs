namespace Innovayse.API.Admin;
using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Servers.Commands.CreateServerGroup;
using Innovayse.Application.Admin.Servers.Commands.DeleteServerGroup;
using Innovayse.Application.Admin.Servers.Commands.UpdateServerGroup;
using Innovayse.Application.Admin.Servers.DTOs;
using Innovayse.Application.Admin.Servers.Queries.ListServerGroups;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Servers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing server groups.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/server-groups")]
[Authorize(Roles = Roles.Admin)]
public sealed class ServerGroupsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all server groups with their assigned servers.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of group DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<List<ServerGroupDto>>> ListAsync(CancellationToken ct) =>
        Ok(await bus.InvokeAsync<List<ServerGroupDto>>(new ListServerGroupsQuery(), ct));

    /// <summary>Creates a new server group.</summary>
    /// <param name="req">Group creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new group ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateServerGroupRequest req, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateServerGroupCommand(req.Name, req.FillType, req.ServerIds), ct);

        return CreatedAtAction(nameof(ListAsync), new { id }, id);
    }

    /// <summary>Updates an existing server group.</summary>
    /// <param name="id">Group identifier.</param>
    /// <param name="req">Updated group data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateServerGroupRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateServerGroupCommand(id, req.Name, req.FillType, req.ServerIds), ct);

        return NoContent();
    }

    /// <summary>Deletes a server group.</summary>
    /// <param name="id">Group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteServerGroupCommand(id), ct);
        return NoContent();
    }
}
