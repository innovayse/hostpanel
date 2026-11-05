using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPage;

/// <summary>Asks for one plugin page of a panel with its view document.</summary>
/// <param name="Area">The panel: client or admin.</param>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="PageId">The page id from the URL.</param>
public sealed record GetPluginPageQuery(PluginArea Area, string PluginId, string PageId);
