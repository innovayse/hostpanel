namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginSlotWidgets;

/// <summary>
/// Lists the slot widgets a loaded plugin declares, each with the products the operator bound it to.
/// </summary>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
public sealed record ListPluginSlotWidgetsQuery(string PluginId = "");
