namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryPlugin;

/// <summary>Reads the details page of one plugin of the plugin registry, set beside what is installed on this server.</summary>
/// <param name="PluginId">The plugin id; set from the route.</param>
public sealed record GetRegistryPluginQuery(string PluginId);
