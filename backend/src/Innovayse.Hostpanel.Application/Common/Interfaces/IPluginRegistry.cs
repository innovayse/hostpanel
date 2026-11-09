using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

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
    public bool IsLoaded(string pluginId);

    /// <summary>
    /// Tells whether the plugin was upgraded and its data migration has not succeeded yet. Such a plugin is not called
    /// and not started: its code would run against data in the old shape.
    /// </summary>
    /// <param name="pluginId">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns><see langword="true"/> while the migration is pending or its last attempt failed.</returns>
    public bool IsMigrationPending(string pluginId);

    /// <summary>
    /// Returns the manifests of all plugins that were successfully loaded at startup.
    /// </summary>
    /// <returns>Read-only collection of loaded plugin manifests.</returns>
    public IReadOnlyCollection<PluginManifest> GetLoadedManifests();

    /// <summary>
    /// Returns the plugins the loader skipped at startup because they break a manifest rule, in the upload folder and
    /// the built-in folder alike.
    /// </summary>
    /// <returns>Read-only collection of refused plugins; empty when none was refused.</returns>
    public IReadOnlyCollection<RefusedPlugin> GetRefusedPlugins();

    /// <summary>
    /// Returns the icon a loaded plugin ships, as it was checked at load.
    /// </summary>
    /// <param name="pluginId">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns>The icon; <see langword="null"/> when the plugin is not loaded or declares none.</returns>
    public PluginIconDto? GetIcon(string pluginId);
}
