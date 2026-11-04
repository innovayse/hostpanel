using System.Text.Json;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One plugin page with its view document, every label in the caller's language.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Id">The page id.</param>
/// <param name="Path">The page path below the plugin's own prefix in the panel.</param>
/// <param name="Title">The title, in the caller's language.</param>
/// <param name="View">The view document (<c>Hostpanel.Sdk.Plugins.Ui.PluginViews</c>), its labels resolved.</param>
/// <param name="ExternalOrigins">
/// The web origins (scheme, host and port, such as <c>https://example.com</c>) the plugin's manifest declares in
/// <c>externalOrigins</c>, as written there; empty when it declares none. The panels need them to render the view's
/// link blocks, which may point at an absolute <c>https</c> address only on one of these origins.
/// </param>
public sealed record PluginPageViewDto(
    string PluginId,
    string Id,
    string Path,
    string Title,
    JsonElement View,
    IReadOnlyList<string> ExternalOrigins);
