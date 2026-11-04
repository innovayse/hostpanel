using System.Text.Json;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One slot widget with its view document, every label in the caller's language.</summary>
/// <param name="PluginId">The plugin that declares the widget.</param>
/// <param name="Id">The widget's id, unique within the plugin and slot.</param>
/// <param name="Title">The title in the request's language; <see langword="null"/> when the widget has none.</param>
/// <param name="View">The view document, its labels resolved, the same shape a plugin page's view has.</param>
/// <param name="ExternalOrigins">
/// The web origins the plugin's manifest declares in <c>externalOrigins</c>, as written there; empty when it declares
/// none. The panels render a link to an absolute <c>https</c> address only on one of these, as on a plugin page.
/// </param>
public sealed record PluginSlotWidgetViewDto(
    string PluginId, string Id, string? Title, JsonElement View, IReadOnlyList<string> ExternalOrigins);
