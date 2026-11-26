using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Reads the view of one widget the slot index lists, the part the client and the admin widget views share: the widget
/// is picked out of an index the area's own handler built for a verified context, so a widget is served only when the
/// index would list it (same gate, same <see cref="PluginSlotIndexBuilder"/>), and its document comes from the views
/// read at load, so no plugin is built or called.
/// </summary>
public sealed class PluginSlotWidgetViewReader
{
    /// <summary>The available plugins with their view documents.</summary>
    private readonly IPluginUiSource _plugins;

    /// <summary>Works out the origins the widget's absolute links may point at for the caller.</summary>
    private readonly PluginFrameOriginResolver _origins;

    /// <summary>Initializes the reader.</summary>
    /// <param name="plugins">The available plugins with their view documents.</param>
    /// <param name="origins">Works out the origins the widget's absolute links may point at for the caller.</param>
    public PluginSlotWidgetViewReader(IPluginUiSource plugins, PluginFrameOriginResolver origins)
    {
        _plugins = plugins;
        _origins = origins;
    }

    /// <summary>Reads the view of the widget the index lists under the given plugin and widget ids.</summary>
    /// <param name="index">The widgets the caller may see in the slot, built for the verified context.</param>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="widgetId">The widget id from the URL.</param>
    /// <param name="area">The panel the caller is in; only the client panel reads the caller's own services for link origins.</param>
    /// <param name="held">The client permissions the viewer holds; not looked at in the admin panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The widget with its view, or <see langword="null"/> when the index does not list it, its plugin went unavailable
    /// since the index was read, or its view document is missing.
    /// </returns>
    public async Task<PluginSlotWidgetViewDto?> ReadAsync(
        PluginSlotIndexDto index, string pluginId, string widgetId, PluginArea area, ClientPermission held, CancellationToken ct)
    {
        var widget = index.Widgets.FirstOrDefault(w =>
            string.Equals(w.PluginId, pluginId, StringComparison.Ordinal)
            && string.Equals(w.Id, widgetId, StringComparison.Ordinal));
        if (widget is null)
        {
            return null;
        }

        // The index already decided the widget shows; the package is read only for its view document and its texts.
        // It can only narrow that answer: a plugin that went unavailable between the two reads answers the same 404,
        // and nothing it holds can widen what the index listed.
        var package = await _plugins.FindAvailableAsync(widget.PluginId, ct);
        if (package?.Views.Localised(widget.View, PluginUiTexts.For(package.Manifest)) is not { } document)
        {
            return null;
        }

        // Same rule as a plugin page: the manifest's origins plus the https host of the caller's own bound services; the
        // admin panel acts for no client, so nothing is read for it.
        IReadOnlyList<string> frameOrigins = area == PluginArea.Client
            ? await _origins.ResolveAsync(package.Manifest, held, ct)
            : [];

        return new PluginSlotWidgetViewDto(
            widget.PluginId,
            widget.Id,
            widget.Title,
            document,
            PluginFrameOriginResolver.LinkOrigins(package.Manifest, frameOrigins));
    }
}
