using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlot;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The index of the widgets plugins put into a client area page slot: <c>/api/client/plugins/slots/{slotId}</c>. The
/// page names the record it shows; the use case verifies it is the caller's own before it lists anything, and no plugin
/// is called.
/// </summary>
/// <remarks>
/// Behind <see cref="Policies.Client"/> and marked <see cref="AvailableToEveryClientUserAttribute"/>: each widget declares
/// its own permission, so the use case filters by what the caller holds instead of refusing the whole index.
/// </remarks>
[Route("api/client/plugins/slots")]
[Authorize(Policy = Policies.Client)]
[AvailableToEveryClientUser]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientPluginSlotsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientPluginSlotsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists the widgets plugins put into a client area slot, for the page the caller is on.</summary>
    /// <param name="slotId">The slot id, such as <c>client.service.tabs</c>; wins over any slot id in the query.</param>
    /// <param name="query">The context of the page: <c>serviceId</c> for a slot that offers it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the widgets to show, empty when there are none; 400 with <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> when the slot
    /// needs a service and none is named; 401 without a session; 403 for a caller who is not a client user; 404 with
    /// <c>PLUGIN_SLOT_NOT_FOUND</c> when the host offers no such client slot, with <c>SERVICE_NOT_FOUND</c> when the service
    /// is not the caller's or does not exist (the same answer for both), or with <c>CLIENT_PROFILE_NOT_FOUND</c> when the
    /// caller acts for no account; 429 when the rate limit is hit; 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the
    /// identity plugin could not say who the caller is.
    /// </returns>
    [HttpGet("{slotId}")]
    [ProducesResponseType(typeof(PluginSlotIndexDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(string slotId, [FromQuery] GetClientPluginSlotQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginSlotIndexDto>>(query with { SlotId = slotId }, ct);
        return HandleResult(outcome);
    }
}
