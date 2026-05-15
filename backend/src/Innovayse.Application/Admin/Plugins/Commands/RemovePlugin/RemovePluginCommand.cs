namespace Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Removes a plugin by deleting its directory from the plugins folder.
/// </summary>
/// <param name="Id">The plugin identifier matching the subdirectory name.</param>
public record RemovePluginCommand(string Id);
