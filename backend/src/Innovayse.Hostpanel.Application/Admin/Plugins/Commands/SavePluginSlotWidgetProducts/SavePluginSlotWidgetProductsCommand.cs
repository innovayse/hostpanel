using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SavePluginSlotWidgetProducts;

/// <summary>
/// Binds one slot widget of a loaded plugin to products after install. A non-empty list replaces the widget manifest
/// <c>productIds</c> from then on; an empty list, and only an empty list, removes the binding so the manifest applies again. Unknown and deleted
/// product ids are dropped before the list is stored.
/// </summary>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
/// <param name="SlotId">The slot id from the URL; the controller sets it from the route.</param>
/// <param name="WidgetId">The widget id from the URL; the controller sets it from the route.</param>
/// <param name="ProductIds">The products the widget shows for; empty clears the binding.</param>
public sealed record SavePluginSlotWidgetProductsCommand(
    string PluginId = "",
    string SlotId = "",
    string WidgetId = "",
    IReadOnlyList<int>? ProductIds = null) : IDemoRestricted;
