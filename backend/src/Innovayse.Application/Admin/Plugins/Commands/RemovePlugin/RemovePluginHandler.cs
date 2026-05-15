namespace Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Application.Admin.Plugins.DTOs;

/// <summary>
/// Handles <see cref="RemovePluginCommand"/> — deletes the plugin directory from disk.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>
    /// Deletes the plugin directory for the given plugin id.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="PluginActionResultDto"/> with <c>RequiresRestart = true</c> when the plugin was removed;
    /// <see langword="null"/> when no plugin with that id is installed.
    /// </returns>
    public Task<PluginActionResultDto?> HandleAsync(RemovePluginCommand command, CancellationToken ct)
    {
        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var pluginDir = Path.GetFullPath(Path.Combine(pluginsRoot, command.Id));

        // Guard against path traversal
        if (!pluginDir.StartsWith(Path.GetFullPath(pluginsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid plugin id: '{command.Id}'.");
        }

        if (!Directory.Exists(pluginDir))
        {
            return Task.FromResult<PluginActionResultDto?>(null);
        }

        Directory.Delete(pluginDir, recursive: true);
        return Task.FromResult<PluginActionResultDto?>(new PluginActionResultDto(RequiresRestart: true));
    }
}
