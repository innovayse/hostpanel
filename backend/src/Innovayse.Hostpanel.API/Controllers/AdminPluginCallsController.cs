using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The one door to the admin area API of every app plugin: <c>/api/admin/plugins/{id}/ext/{path}</c>. It binds the
/// request and dispatches it; the use case matches it against the routes the plugin's manifest declares and calls
/// the plugin. The admin policy is the whole authorisation: an admin route declares no permission.
/// </summary>
/// <remarks>
/// A request that changes state needs <c>X-Requested-With: XMLHttpRequest</c> or a bearer token (<see cref="RequireRequestedWithAttribute"/>).
/// A plugin route that is undeclared, disabled or unknown answers 404 <c>PLUGIN_ROUTE_NOT_FOUND</c>, always the same.
/// The <c>ext</c> segment keeps these routes apart from the plugin administration routes beside them
/// (<c>install</c>, <c>restart</c> and the removal of a plugin by id). The body, and the status of a success, are
/// the plugin's, as on the client area door.
/// </remarks>
[Route("api/admin/plugins")]
[Authorize(Policy = Policies.Admin)]
[RequireRequestedWith]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class AdminPluginCallsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public AdminPluginCallsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Calls a route of an app plugin in the admin area.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="path">The path below the plugin's admin prefix, matched against its declared routes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's own success (200, 201, 202, 203 with its JSON, or 204); 400, 409, 422 or any other failure
    /// status the plugin chooses, with <c>plugin.{id}.{code}</c> as the code; 401 without a session; 403 for a caller who is
    /// not an administrator, or with <c>CSRF_HEADER_MISSING</c> when a request that changes state has neither the header
    /// nor a bearer token; 404 with <c>PLUGIN_ROUTE_NOT_FOUND</c> when the plugin, the route or the plugin's switch is not
    /// there; 413 with <c>PLUGIN_REQUEST_TOO_LARGE</c> when the body is over <c>PluginApi:MaxBodyBytes</c>; 429 when the
    /// rate limit is hit; 501 with <c>PLUGIN_STREAMING_NOT_SUPPORTED</c> for a streaming route; 502 with
    /// <c>PLUGIN_FAILED</c> when the plugin threw; 503 when the plugin chooses it.
    /// </returns>
    [HttpGet("{id}/ext/{**path}")]
    [HttpPost("{id}/ext/{**path}")]
    [HttpPut("{id}/ext/{**path}")]
    [HttpPatch("{id}/ext/{**path}")]
    [HttpDelete("{id}/ext/{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status203NonAuthoritative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CallAsync(string id, string? path, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginReplyDto>>(
            new CallAdminPluginCommand(id, PluginHttpInputReader.Read(Request, path)), ct);
        return HandleForwarded(outcome);
    }
}
