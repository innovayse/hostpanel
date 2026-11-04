namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetPluginIcon;

/// <summary>
/// Reads the icon of a loaded plugin.
/// </summary>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
public sealed record GetPluginIconQuery(string PluginId = "");
