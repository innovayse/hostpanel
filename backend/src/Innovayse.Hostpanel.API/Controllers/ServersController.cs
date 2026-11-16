using Innovayse.Hostpanel.Application.Admin.Servers.Commands.ConfigureServerSsh;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.CreateServer;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.DeleteServer;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.GenerateServerSshIdentity;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.ScanServerSshHostKey;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.TestServerConnection;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.TestServerSshAgent;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.UpdateServer;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshSettings;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshSetup;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerOptions;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;
using Innovayse.Hostpanel.Application.Services.Commands.RevokeServerSshKeys;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Staff administration of the provisioning servers hosting accounts are placed on.</summary>
[Route("api/admin/servers")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ServersController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ServersController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>One page of the configured provisioning servers, by name, narrowed by a search text, a module and a last known state.</summary>
    /// <param name="query">The page, page size, search, module and state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ServerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListServersQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ServerDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Every configured provisioning server as a lean option, by name, optionally filtered by module: the picker the group
    /// form, the add existing service dialog, the service detail, the CWP 7 integration and the dashboard read whole.
    /// </summary>
    /// <param name="query">The optional module filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the servers; 400 when the module is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("options")]
    [ProducesResponseType(typeof(IReadOnlyList<ServerOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListOptionsAsync([FromQuery] ListServerOptionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ServerOptionDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new provisioning server.</summary>
    /// <param name="command">The server's configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new server's id; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateServerCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/servers/{id}", id));
    }

    /// <summary>Updates an existing server's configuration.</summary>
    /// <param name="id">Server primary key; wins over any id in the body.</param>
    /// <param name="command">The updated configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no server;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateServerCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a server.</summary>
    /// <param name="id">Server primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no server; 409 with
    /// <c>SERVER_IN_USE</c> when a client service or an SSH key still references it;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteServerCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Tests connectivity to a server using its stored credentials and persists the result.</summary>
    /// <param name="id">Server primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the connection test result, whether or not the connection itself succeeded;
    /// 400 when a value is refused; 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/test-connection")]
    [ProducesResponseType(typeof(TestServerConnectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnectionAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TestServerConnectionResultDto>>(
            new TestServerConnectionCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Gets a server's SSH key management settings. The stored private key is never returned.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the settings, disabled defaults when never configured; 400 when a value is refused;
    /// 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/ssh-settings")]
    [ProducesResponseType(typeof(ServerSshSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSshSettingsAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServerSshSettingsDto>>(new GetServerSshSettingsQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates or replaces a server's SSH key management settings.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="command">The settings; the server id comes from the route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the saved settings;
    /// 400 when a value is refused, the module has no key agent or the settings are incomplete;
    /// 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/ssh-settings")]
    [ProducesResponseType(typeof(ServerSshSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConfigureSshSettingsAsync(
        int id, [FromBody] ConfigureServerSshCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServerSshSettingsDto>>(command with { ServerId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Pings the server's SSH key agent and records the health outcome.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the outcome, an unhealthy agent included;
    /// 400 when the id is refused, the module has no key agent or the settings are incomplete;
    /// 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/ssh-settings/test")]
    [ProducesResponseType(typeof(TestServerSshAgentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestSshAgentAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TestServerSshAgentResultDto>>(new TestServerSshAgentCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Gets what is needed to install the SSH key agent on the server: profile, public key, binding and bundle.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the setup, which never carries the private key; 400 when the id is refused;
    /// 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/ssh-setup")]
    [ProducesResponseType(typeof(ServerSshSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSshSetupAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServerSshSetupDto>>(new GetServerSshSetupQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Generates (or rotates) the server's ED25519 management identity. Rotation switches customer
    /// access off until the installer runs again.
    /// </summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the setup carrying the new public key; 400 when the id is refused or the module has no
    /// key agent; 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/ssh-identity")]
    [ProducesResponseType(typeof(ServerSshSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateSshIdentityAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServerSshSetupDto>>(new GenerateServerSshIdentityCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Reads the host key the saved agent endpoint presents. Shown for verification, never pinned by this call.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the presented key and whether it matches the pin; 400 when the id is refused or no
    /// agent host is saved; 404 when the id names no server; 503 when the endpoint gave no host key,
    /// with a code naming what to check; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/ssh-settings/scan-host-key")]
    [ProducesResponseType(typeof(SshHostKeyScanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ScanSshHostKeyAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SshHostKeyScanDto>>(new ScanServerSshHostKeyCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Revokes every HostPanel-managed SSH key issued on the server, for example before uninstalling its agent.</summary>
    /// <param name="id">Server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with how many keys were removed and how many could not be confirmed; 400 when the id is
    /// refused; 404 when the id names no server; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/ssh-keys/revoke-all")]
    [ProducesResponseType(typeof(RevokeServerSshKeysResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeServerSshKeysAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RevokeServerSshKeysResultDto>>(new RevokeServerSshKeysCommand(id), ct);
        return HandleResult(outcome);
    }
}
