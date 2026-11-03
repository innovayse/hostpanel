using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Removes an uploaded plugin: deletes its folder from the upload folder when it is still there, switches the plugin
/// off and purges the data it kept in the plugin store. A plugin whose folder is already gone is still switched off and
/// purged; a built-in plugin or an id breaking the plugin id rule is refused and nothing is touched.
/// </summary>
/// <param name="Id">The plugin identifier matching the subdirectory name.</param>
public sealed record RemovePluginCommand(string Id) : IDemoRestricted;
