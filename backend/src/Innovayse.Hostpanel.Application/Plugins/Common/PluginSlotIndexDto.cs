namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The widgets plugins put into one slot for one verified context.</summary>
/// <param name="SlotId">The slot's id, such as <c>client.service.tabs</c>.</param>
/// <param name="Widgets">The widgets to show, ordered by <c>order</c>, then plugin id, then widget id; empty when there are none.</param>
public sealed record PluginSlotIndexDto(string SlotId, IReadOnlyList<PluginSlotWidgetDto> Widgets);
