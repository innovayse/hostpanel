namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One product an operator bound a plugin slot widget to, as the slot index and the admin list read it.</summary>
/// <param name="PluginId">The plugin that declares the widget.</param>
/// <param name="SlotId">The slot the widget is placed in.</param>
/// <param name="WidgetId">The widget id within the plugin.</param>
/// <param name="ProductId">The product the widget was bound to.</param>
/// <param name="ProductExists">
/// Whether that product still exists. A row of a deleted product is still returned, so a widget the operator bound is
/// told apart from one never bound: it counts as bound with no product left and shows nowhere instead of falling back
/// to the manifest.
/// </param>
public sealed record PluginSlotBindingDto(string PluginId, string SlotId, string WidgetId, int ProductId, bool ProductExists);
