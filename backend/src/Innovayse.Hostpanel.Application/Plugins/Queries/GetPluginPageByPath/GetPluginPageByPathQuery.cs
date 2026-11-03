using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPageByPath;

/// <summary>Asks for the plugin page of a panel that a path below the plugin's prefix names, with its view document.</summary>
/// <param name="Area">The panel: client or admin.</param>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="Path">
/// The page path from the query string, such as <c>/sites/42</c>; <see langword="null"/> when the query string has none,
/// which is answered like an unknown page.
/// </param>
public sealed record GetPluginPageByPathQuery(PluginArea Area, string PluginId, string? Path);
