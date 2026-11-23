namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginServers;

/// <summary>
/// Lists the servers a loaded panel plugin serves: the ones stored on a server module its manifest declares.
/// </summary>
/// <param name="PluginId">The plugin id from the URL; the controller sets it from the route.</param>
public sealed record ListPluginServersQuery(string PluginId = "");
