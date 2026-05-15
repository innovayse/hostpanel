namespace Innovayse.API.Admin;
using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Servers.Commands.CreateServer;
using Innovayse.Application.Admin.Servers.Commands.DeleteServer;
using Innovayse.Application.Admin.Servers.Commands.UpdateServer;
using Innovayse.Application.Admin.Servers.DTOs;
using Innovayse.Application.Admin.Servers.Queries.ListServers;
using Innovayse.Application.Admin.Servers.Queries.TestServerConnection;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing provisioning servers.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/servers")]
[Authorize(Roles = Roles.Admin)]
public sealed class ServersController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all provisioning servers.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of server DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<List<ServerDto>>> ListAsync(CancellationToken ct) =>
        Ok(await bus.InvokeAsync<List<ServerDto>>(new ListServersQuery(null), ct));

    /// <summary>Creates a new provisioning server.</summary>
    /// <param name="req">Server creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new server ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateServerRequest req, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(new CreateServerCommand(req.ToDetails()), ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing server.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="req">Updated server data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateServerRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateServerCommand(id, req.ToDetails()), ct);
        return NoContent();
    }

    /// <summary>Deletes a server.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteServerCommand(id), ct);
        return NoContent();
    }

    /// <summary>Tests connectivity to a server and persists the result.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection test result.</returns>
    [HttpPost("{id:int}/test-connection")]
    public async Task<ActionResult<TestServerConnectionResultDto>> TestConnectionAsync(int id, CancellationToken ct) =>
        Ok(await bus.InvokeAsync<TestServerConnectionResultDto>(new TestServerConnectionQuery(id), ct));
}
