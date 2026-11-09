using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPage;
using Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPageByPath;
using Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginUi;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The plugin pages and menu entries of the client and admin panels, and each page's view document, every label in the
/// caller's language (<c>X-Language</c>). The panels render what these answer and never filter or translate plugin UI
/// themselves.
/// </summary>
/// <remarks>
/// The <c>ui</c> segment is reserved as a plugin id, so these routes never collide with the plugin doors
/// (<c>/api/client/plugins/{id}/{path}</c>, <c>/api/admin/plugins/{id}</c>). A plugin that is unknown, switched off, not
/// a serving plugin or does not declare the page answers the same 404 <c>PLUGIN_PAGE_NOT_FOUND</c>, and so does a page the
/// client caller may not open, so a refusal never tells a page that exists from one that does not.
/// A page is also found by its path (<c>pages/by-path?path=/sites/42</c>), the address a panel shows for it. The
/// literal <c>by-path</c> wins over <c>{pageId}</c> in routing, and <c>by-path</c> is a reserved page id
/// (<c>PluginPageIdentifier.Reserved</c>), so no installed page can be shadowed by it.
/// </remarks>
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PluginUiController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PluginUiController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the plugin menu entries and pages of the client panel that the caller may open.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the index, empty lists with no plugin; 401 without a session; 403 for a caller who is not a client user;
    /// 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account; 429 when the rate limit is hit;
    /// 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller is.
    /// </returns>
    [HttpGet("api/client/plugins/ui")]
    [Authorize(Policy = Policies.Client)]
    [AvailableToEveryClientUser]
    [ProducesResponseType(typeof(PluginUiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientUiAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginUiDto>>(new GetPluginUiQuery(PluginArea.Client), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns one client panel plugin page with its view document.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 401 without a session; 403 from the client portal policy for a caller who is not a client user;
    /// 404 with <c>PLUGIN_PAGE_NOT_FOUND</c> for every refusal of the page itself (an id that is not well formed, unknown,
    /// unavailable or undeclared, no account, or the page's permission not held), one body whatever the reason; 429 when the rate limit is hit; 503
    /// with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller is.
    /// </returns>
    [HttpGet("api/client/plugins/ui/{pluginId}/pages/{pageId}")]
    [Authorize(Policy = Policies.Client)]
    [AvailableToEveryClientUser]
    [ProducesResponseType(typeof(PluginPageViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientPageAsync(string pluginId, string pageId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginPageViewDto>>(
            new GetPluginPageQuery(PluginArea.Client, pluginId, pageId), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the client panel plugin page a path names, with its view document.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The page path below the plugin's prefix, such as <c>/sites/42</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page, the very body the lookup by id answers; 401 without a session; 403 from the client portal policy
    /// for a caller who is not a client user; 404 with <c>PLUGIN_PAGE_NOT_FOUND</c> for every refusal of the page itself
    /// (a missing or malformed path or plugin id, an unknown, unavailable or undeclared page, no account, or the page's
    /// permission not held), one body whatever the reason; 429 when the rate limit is hit; 503 with
    /// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller is.
    /// </returns>
    [HttpGet("api/client/plugins/ui/{pluginId}/pages/by-path")]
    [Authorize(Policy = Policies.Client)]
    [AvailableToEveryClientUser]
    [ProducesResponseType(typeof(PluginPageViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientPageByPathAsync(
        string pluginId, [FromQuery] string? path, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginPageViewDto>>(
            new GetPluginPageByPathQuery(PluginArea.Client, pluginId, path), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the plugin menu entries and pages of the admin panel.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the index, empty lists with no plugin; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("api/admin/plugins/ui")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PluginUiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminUiAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginUiDto>>(new GetPluginUiQuery(PluginArea.Admin), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns one admin panel plugin page with its view document.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 401 without a session; 403 for a caller who is not an admin; 404 with
    /// <c>PLUGIN_PAGE_NOT_FOUND</c> for an id that is not well formed and for an unknown, unavailable or undeclared page
    /// alike; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("api/admin/plugins/ui/{pluginId}/pages/{pageId}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PluginPageViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminPageAsync(string pluginId, string pageId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginPageViewDto>>(
            new GetPluginPageQuery(PluginArea.Admin, pluginId, pageId), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the admin panel plugin page a path names, with its view document.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The page path below the plugin's prefix, such as <c>/reports</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page, the very body the lookup by id answers; 401 without a session; 403 for a caller who is not an
    /// admin; 404 with <c>PLUGIN_PAGE_NOT_FOUND</c> for a missing or malformed path or plugin id and for an unknown,
    /// unavailable or undeclared page alike; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("api/admin/plugins/ui/{pluginId}/pages/by-path")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PluginPageViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminPageByPathAsync(
        string pluginId, [FromQuery] string? path, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginPageViewDto>>(
            new GetPluginPageByPathQuery(PluginArea.Admin, pluginId, path), ct);
        return HandleResult(outcome);
    }
}
