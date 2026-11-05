using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Plugins.Commands.CallPluginCallback;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The public door for plugin callbacks: <c>/api/plugins/{id}/callback/{path}</c>, where a payment gateway or another
/// external service calls the panel server to server without a session (a payment provider webhook, a pre check, a
/// bank result URL). It binds the request, raw body and headers unchanged, and dispatches it; the use case answers only
/// for a route the plugin declares under <c>api.callbacks</c> in its manifest, and the plugin verifies the call itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonymous on purpose, and closed by default.</b> There is no sign-in, no CSRF header (no cookie authorises anything
/// here) and no account: a plugin that is unknown, disabled, not configured, or does not declare the route answers the
/// same 404 <c>PLUGIN_ROUTE_NOT_FOUND</c>, whoever asks. The body is read whole within <c>PluginApi:MaxBodyBytes</c> and
/// handed over byte for byte, so a signature over it still verifies. Every request to the door is counted against
/// the callback tier per caller address (<c>RateLimit:PluginCallback</c>), declared or not.
/// </para>
/// <para>
/// The client and admin routes of a plugin are never reachable here, and a callback route is never reachable from the
/// signed-in doors. Failures are logged by plugin id only, never with a header, a query or a body.
/// </para>
/// </remarks>
[Route("api/plugins")]
[AllowAnonymous]
[PluginBodyLimit]
[PluginCallbackRateLimit]
[NoFormBinding]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PluginCallbacksController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PluginCallbacksController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Delivers a callback to a route an app plugin declares under <c>api.callbacks</c>.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="path">The path below <c>callback/</c>, matched against the plugin's declared callback routes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's own success (200, 201, 202, 203 with its JSON, or 204), or a raw body with the status and media type it
    /// chose (the acknowledgement a gateway waits for); 400, 409, 422 or any other failure status the
    /// plugin chooses, with <c>plugin.{id}.{code}</c> as the code, which is how a plugin tells the gateway its signature
    /// did not verify; 404 with <c>PLUGIN_ROUTE_NOT_FOUND</c> when the plugin, the route, the method or the plugin's switch
    /// is not there, always the same; 413 with <c>PLUGIN_REQUEST_TOO_LARGE</c> when the body is over <c>PluginApi:MaxBodyBytes</c>;
    /// 429 when the caller address's callback budget is spent; 502 with <c>PLUGIN_FAILED</c> when the plugin threw; 504 with
    /// <c>PLUGIN_TIMEOUT</c> when it did not answer within <c>PluginApi:CallTimeoutSeconds</c>.
    /// </returns>
    [HttpGet("{id}/callback/{**path}")]
    [HttpPost("{id}/callback/{**path}")]
    [HttpPut("{id}/callback/{**path}")]
    [HttpPatch("{id}/callback/{**path}")]
    [HttpDelete("{id}/callback/{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status203NonAuthoritative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> CallAsync(string id, string? path, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginReplyDto>>(
            new CallPluginCallbackCommand(id, PluginHttpInputReader.ReadCallback(Request, path)), ct);
        return HandleForwarded(outcome);
    }
}
