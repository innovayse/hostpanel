using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SavePluginSlotWidgetProducts;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginSlotWidgets;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The slot widgets of an installed plugin and the products the operator binds them to after install:
/// <c>/api/admin/plugins/{pluginId}/slot-widgets</c>. The bindings are written only here.
/// </summary>
[Route("api/admin/plugins/{pluginId}/slot-widgets")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PluginSlotWidgetsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PluginSlotWidgetsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists the slot widgets a loaded plugin declares with their current product binding.</summary>
    /// <param name="pluginId">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the widgets (<c>slotId</c>, <c>widgetId</c>, <c>title</c>, <c>manifestProductIds</c>, <c>productIds</c>, <c>isBound</c>), empty
    /// when the plugin declares none; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403 for a caller who is not an
    /// administrator; 404 with <c>PLUGIN_NOT_FOUND</c> when no loaded plugin has that id; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PluginSlotWidgetBindingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(string pluginId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<PluginSlotWidgetBindingDto>>>(new ListPluginSlotWidgetsQuery(pluginId), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Binds a slot widget to products: a non-empty list replaces the manifest <c>productIds</c> for that widget, an empty
    /// list clears the binding. Unknown and deleted product ids are dropped; when ids are sent and none exists the save is refused.
    /// </summary>
    /// <param name="pluginId">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="slotId">The slot the widget is placed in.</param>
    /// <param name="widgetId">The widget id within the plugin.</param>
    /// <param name="command">The body: <c>productIds</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the widget and the products that were stored; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403
    /// for a caller who is not an administrator; 400 with <c>PLUGIN_SLOT_PRODUCTS_UNKNOWN</c> when none of the products exists; 404 with <c>PLUGIN_NOT_FOUND</c> or <c>PLUGIN_SLOT_WIDGET_NOT_FOUND</c>;
    /// 409 when a competing save won the race twice; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{slotId}/{widgetId}")]
    [ProducesResponseType(typeof(PluginSlotWidgetBindingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveAsync(
        string pluginId, string slotId, string widgetId, [FromBody] SavePluginSlotWidgetProductsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginSlotWidgetBindingDto>>(
            command with { PluginId = pluginId, SlotId = slotId, WidgetId = widgetId }, ct);
        return HandleResult(outcome);
    }
}
