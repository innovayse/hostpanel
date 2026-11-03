namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlot;

/// <summary>
/// Lists the widgets plugins put into an admin area slot for the page the administrator is on. The context names the
/// record the page shows; the host verifies it exists before it lists anything.
/// </summary>
/// <param name="SlotId">The slot id from the URL, such as <c>admin.client.panels</c>; the controller sets it from the route.</param>
/// <param name="ServiceId">The service the page shows, for a slot that offers <c>serviceId</c>; ignored by a slot that does not.</param>
/// <param name="ClientId">The client the page shows, for a slot that offers <c>clientId</c>; on a service slot it must be the service's own client.</param>
public sealed record GetAdminPluginSlotQuery(string SlotId = "", int? ServiceId = null, int? ClientId = null);
