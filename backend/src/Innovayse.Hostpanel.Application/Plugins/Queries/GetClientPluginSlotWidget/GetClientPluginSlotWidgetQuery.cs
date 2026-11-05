namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlotWidget;

/// <summary>
/// Reads the view document of one widget of a client area slot, for the page the caller is on. Each widget is read on its
/// own so a slow or broken one never holds up the page or the others.
/// </summary>
/// <param name="SlotId">The slot id from the URL; the controller sets it from the route.</param>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
/// <param name="WidgetId">The widget id from the URL; the controller sets it from the route.</param>
/// <param name="ServiceId">
/// The service the page shows, for a slot that offers <c>serviceId</c>, as the query string carries it. Text on purpose:
/// a value that is not a positive whole number must answer the same 404 as every other refusal, and an <c>int</c> would
/// be refused by model binding with a 400 before the use case runs. The handler parses it.
/// </param>
public sealed record GetClientPluginSlotWidgetQuery(
    string SlotId = "", string PluginId = "", string WidgetId = "", string? ServiceId = null);
