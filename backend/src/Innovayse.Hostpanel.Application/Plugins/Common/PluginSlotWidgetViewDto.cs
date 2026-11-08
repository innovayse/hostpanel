using System.Text.Json;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One slot widget with its view document, every label in the caller's language.</summary>
/// <param name="PluginId">The plugin that declares the widget.</param>
/// <param name="Id">The widget's id, unique within the plugin and slot.</param>
/// <param name="Title">The title in the request's language; <see langword="null"/> when the widget has none.</param>
/// <param name="View">The view document, its labels resolved, the same shape a plugin page's view has.</param>
/// <param name="ExternalOrigins">
/// The web origins the widget's absolute links may point at, worked out as on a plugin page (the manifest's, then the
/// https origins of the caller's own bound services); empty when there are none. The panels render a link to an absolute
/// <c>https</c> address only on one of these.
/// </param>
public sealed record PluginSlotWidgetViewDto(
    string PluginId, string Id, string? Title, JsonElement View, IReadOnlyList<string> ExternalOrigins);
