using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Removes an uploaded plugin: deletes its folder (and any upgrade staged for it) from the upload folder when it is
/// still there and switches the plugin off. The data it kept in the plugin store stays until
/// <c>PurgePluginDataCommand</c>. A plugin whose folder is already gone is still switched off; a built-in plugin or an id breaking the plugin id rule is refused and nothing is touched.
/// </summary>
/// <param name="Id">The plugin identifier matching the subdirectory name.</param>
public sealed record RemovePluginCommand(string Id) : IDemoRestricted;
