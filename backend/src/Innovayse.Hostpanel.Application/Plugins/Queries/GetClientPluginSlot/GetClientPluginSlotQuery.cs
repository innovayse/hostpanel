namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlot;

/// <summary>
/// Lists the widgets plugins put into a client area slot for the page the caller is on. The context names the record the
/// page shows; the host verifies it belongs to the caller before it lists anything.
/// </summary>
/// <param name="SlotId">The slot id from the URL, such as <c>client.service.tabs</c>; the controller sets it from the route.</param>
/// <param name="ServiceId">The service the page shows, for a slot that offers <c>serviceId</c>; ignored by a slot that does not.</param>
public sealed record GetClientPluginSlotQuery(string SlotId = "", int? ServiceId = null);
