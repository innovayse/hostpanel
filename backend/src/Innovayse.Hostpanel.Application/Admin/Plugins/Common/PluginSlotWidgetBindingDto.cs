namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// One slot widget of an installed plugin with the products it is bound to, as the admin plugin page lists it.
/// </summary>
/// <param name="SlotId">The slot the widget is placed in, such as <c>admin.service.panels</c>.</param>
/// <param name="WidgetId">The widget's id within the plugin.</param>
/// <param name="Title">The widget's title in the request's language; <see langword="null"/> when it has none.</param>
/// <param name="ManifestProductIds">The products the plugin's manifest binds the widget to; empty means every service.</param>
/// <param name="ProductIds">The products the operator bound the widget to, existing ones only.</param>
/// <param name="IsBound">
/// Whether the operator bound the widget at all. When <see langword="true"/> the operator binding decides where the widget
/// shows and <paramref name="ManifestProductIds"/> are ignored, even when <paramref name="ProductIds"/> is empty because
/// every bound product was deleted (the widget then shows nowhere). When <see langword="false"/> the manifest binding applies.
/// </param>
public sealed record PluginSlotWidgetBindingDto(
    string SlotId,
    string WidgetId,
    string? Title,
    IReadOnlyList<int> ManifestProductIds,
    IReadOnlyList<int> ProductIds,
    bool IsBound);
