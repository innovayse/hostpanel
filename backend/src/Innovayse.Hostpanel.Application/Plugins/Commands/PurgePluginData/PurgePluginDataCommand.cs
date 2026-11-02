namespace Innovayse.Hostpanel.Application.Plugins.Commands.PurgePluginData;

/// <summary>
/// Deletes every row a plugin keeps in the host-kept plugin store, in every scope (each client reference and the
/// plugin-wide rows). Dispatched by <c>RemovePluginHandler</c> after an uninstall, so a removed plugin leaves no data
/// behind. Deleting nothing is a success, which makes a repeat harmless.
/// </summary>
/// <param name="PluginId">The id of the plugin whose store rows are deleted.</param>
public sealed record PurgePluginDataCommand(string PluginId);
