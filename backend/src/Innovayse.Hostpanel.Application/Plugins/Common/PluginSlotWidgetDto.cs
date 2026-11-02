namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One widget a plugin puts into a slot, as the page needs it to place the widget.</summary>
/// <param name="PluginId">The plugin that declares the widget.</param>
/// <param name="Id">The widget's id, unique within the plugin.</param>
/// <param name="View">The path, inside the plugin package, of the document that describes the content.</param>
/// <param name="Title">The title in the request's language; <see langword="null"/> when the widget has none.</param>
/// <param name="Order">The position among the widgets of the slot, lowest first.</param>
/// <param name="ContextKeys">The context keys the widget receives, such as <c>serviceId</c>; the host verified them.</param>
public sealed record PluginSlotWidgetDto(
    string PluginId, string Id, string View, string? Title, int Order, IReadOnlyList<string> ContextKeys);
