using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlot;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The index of the widgets plugins put into an admin area page slot: <c>/api/admin/plugins/slots/{slotId}</c>. The page
/// names the record it shows; the use case verifies it exists before it lists anything, and no plugin is called.
/// </summary>
[Route("api/admin/plugins/slots")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class AdminPluginSlotsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public AdminPluginSlotsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists the widgets plugins put into an admin area slot, for the page the administrator is on.</summary>
    /// <param name="slotId">The slot id, such as <c>admin.service.panels</c>; wins over any slot id in the query.</param>
    /// <param name="query">The context of the page: <c>serviceId</c> and <c>clientId</c> for the slots that offer them.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the widgets to show, empty when there are none; 400 with <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> when the slot
    /// needs a service or client and none is named; 401 without a session; 403 for a caller who is not an administrator;
    /// 404 with <c>PLUGIN_SLOT_NOT_FOUND</c> when the host offers no such admin slot, with <c>SERVICE_NOT_FOUND</c> when the
    /// service does not exist or the client named with it is not its own, or with <c>CLIENT_NOT_FOUND</c> when the client
    /// does not exist; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{slotId}")]
    [ProducesResponseType(typeof(PluginSlotIndexDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(string slotId, [FromQuery] GetAdminPluginSlotQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginSlotIndexDto>>(query with { SlotId = slotId }, ct);
        return HandleResult(outcome);
    }
}
