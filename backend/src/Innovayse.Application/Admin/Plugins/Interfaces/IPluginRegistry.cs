namespace Innovayse.Application.Admin.Plugins.Interfaces;

using Innovayse.SDK.Plugins;

/// <summary>
/// Provides read access to the set of plugins that were successfully loaded at startup.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Determines whether a plugin was successfully loaded during application startup.
    /// </summary>
    /// <param name="pluginId">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns><see langword="true"/> if the plugin DLL was loaded; otherwise <see langword="false"/>.</returns>
    bool IsLoaded(string pluginId);

    /// <summary>
    /// Returns the manifests of all plugins that were successfully loaded at startup.
    /// </summary>
    /// <returns>Read-only collection of loaded plugin manifests.</returns>
    IReadOnlyCollection<PluginManifest> GetLoadedManifests();
}
