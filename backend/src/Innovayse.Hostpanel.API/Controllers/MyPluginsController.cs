using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetPluginIcon;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// What a signed-in client may read of the installed plugins: the icon of the plugin that serves a service's panel, so the portal's
/// Open panel button can show it. It reads the one icon query the admin route uses; nothing else about a plugin is exposed.
/// </summary>
[Route("api/me/plugins")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyPluginsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyPluginsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// The icon an installed plugin ships, served from memory with <c>X-Content-Type-Options: nosniff</c>, a restrictive
    /// Content-Security-Policy and an entity tag.
    /// </summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the image (<c>image/svg+xml</c> or <c>image/png</c>); 304 when the caller's <c>If-None-Match</c> holds
    /// the current tag; 400 with <c>VALIDATION_ERROR</c> for an id that is not a plugin id; 401 without a session; 403
    /// for a caller who is not a client or lacks <c>AllowSingleSignOn</c> (the permission of the button the icon belongs to); 404 with <c>PLUGIN_ICON_NOT_FOUND</c> alike for an unknown plugin and
    /// for a plugin without an icon; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id}/icon")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.AllowSingleSignOn)]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "image/svg+xml", "image/png")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIconAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginIconDto>>(new GetPluginIconQuery(id), ct);
        return HandleResult(outcome, icon => InlineImage(icon.Content, icon.ContentType, icon.ETag));
    }
}
