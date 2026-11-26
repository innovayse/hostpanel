namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlotWidget;

/// <summary>
/// Reads the view document of one widget of an admin area slot, for the page the administrator is on. Each widget is
/// read on its own so a slow or broken one never holds up the page or the others.
/// </summary>
/// <param name="SlotId">The slot id from the URL, such as <c>admin.service.panels</c>; the controller sets it from the route.</param>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
/// <param name="WidgetId">The widget id from the URL; the controller sets it from the route.</param>
/// <param name="ServiceId">The service the page shows, for a slot that offers <c>serviceId</c>; ignored by a slot that does not.</param>
/// <param name="ClientId">The client the page shows, for a slot that offers <c>clientId</c>; on a service slot it must be the service's own client.</param>
public sealed record GetAdminPluginSlotWidgetQuery(
    string SlotId = "", string PluginId = "", string WidgetId = "", int? ServiceId = null, int? ClientId = null);
