using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginServers;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The servers an installed panel plugin serves: <c>/api/admin/plugins/{pluginId}/servers</c>. A read of the stored servers
/// narrowed by the server modules the plugin declares; the connection itself is edited on the Servers page.
/// </summary>
[Route("api/admin/plugins/{pluginId}/servers")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PluginServersController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PluginServersController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists the servers stored on the server modules a loaded plugin declares.</summary>
    /// <param name="pluginId">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the plugin's <c>modules</c> and its <c>servers</c> (name, host name, address, <c>isOnline</c>, <c>lastTestedAt</c>, account
    /// count and limits), both empty for a plugin that serves no server module; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403
    /// for a caller who is not an administrator; 404 with <c>PLUGIN_NOT_FOUND</c> when no loaded plugin has that id; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PluginServersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(string pluginId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginServersDto>>(new ListPluginServersQuery(pluginId), ct);
        return HandleResult(outcome);
    }
}
