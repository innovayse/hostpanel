using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Plugins.Commands.CallClientPlugin;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The one door to the client area API of every app plugin: <c>/api/client/plugins/{id}/{path}</c>. It binds the
/// request and dispatches it; the use case matches it against the routes the plugin's manifest declares, checks the
/// caller's account and the route's permission, and only then calls the plugin.
/// </summary>
/// <remarks>
/// <para>
/// Behind <see cref="Policies.Client"/>, and marked <see cref="AvailableToEveryClientUserAttribute"/> because the permission
/// is not known here: it is the one the plugin declares for the matched route, so the use case enforces it from
/// <c>ICallerClientResolver</c>, the same source <c>RequiresClientPermissionAttribute</c> asks. A route that declares
/// none is for the account holder alone.
/// </para>
/// <para>
/// A request that changes state needs <c>X-Requested-With: XMLHttpRequest</c> or a bearer token (<see cref="RequireRequestedWithAttribute"/>).
/// A plugin route that is undeclared, disabled or unknown answers 404 <c>PLUGIN_ROUTE_NOT_FOUND</c>, always the same.
/// The body, and the status of a success, are the plugin's: the document lists the statuses a plugin may choose.
/// A plugin's own problem keeps the status the plugin chose (any 4xx or 5xx except 401 and 407) and its code is
/// written <c>plugin.{pluginId}.{code}</c>.
/// </para>
/// </remarks>
[Route("api/client/plugins")]
[Authorize(Policy = Policies.Client)]
[AvailableToEveryClientUser]
[RequireRequestedWith]
[PluginBodyLimit]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientPluginCallsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientPluginCallsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Calls a route of an app plugin in the client area.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="path">The path below the plugin's prefix, matched against its declared routes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's own success (200, 201, 202, 203 with its JSON, or 204); 400, 409, 422 or any other failure
    /// status the plugin chooses, with <c>plugin.{id}.{code}</c> as the code; 401 without a session; 403 with
    /// <c>CSRF_HEADER_MISSING</c> when a request that changes state has neither the header nor a bearer token, with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the caller lacks the route's permission, or for a caller who is not a
    /// client user; 404 with <c>PLUGIN_ROUTE_NOT_FOUND</c> when the plugin, the route or the plugin's switch is not there,
    /// or with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account; 413 with <c>PLUGIN_REQUEST_TOO_LARGE</c>
    /// when the body is over <c>PluginApi:MaxBodyBytes</c>; 429 when the rate limit is hit; 501 with
    /// <c>PLUGIN_STREAMING_NOT_SUPPORTED</c> for a streaming route; 502 with <c>PLUGIN_FAILED</c> when the plugin threw;
    /// 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller is.
    /// </returns>
    [HttpGet("{id}/{**path}")]
    [HttpPost("{id}/{**path}")]
    [HttpPut("{id}/{**path}")]
    [HttpPatch("{id}/{**path}")]
    [HttpDelete("{id}/{**path}")]
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
    public async Task<IActionResult> CallAsync(string id, string? path, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginReplyDto>>(
            new CallClientPluginCommand(id, PluginHttpInputReader.Read(Request, path)), ct);
        return HandleForwarded(outcome);
    }
}
