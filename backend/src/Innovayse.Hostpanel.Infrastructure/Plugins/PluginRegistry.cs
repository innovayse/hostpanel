using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Tracks all plugins that were successfully loaded during application startup.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    /// <summary>Loaded plugin entries, keyed by plugin id.</summary>
    private readonly Dictionary<string, LoadedPlugin> _plugins = [];

    /// <summary>
    /// Gets all successfully loaded plugins.
    /// </summary>
    public IReadOnlyCollection<LoadedPlugin> LoadedPlugins => _plugins.Values;

    /// <summary>
    /// Registers a loaded plugin.
    /// </summary>
    /// <param name="plugin">The loaded plugin entry to register.</param>
    internal void Register(LoadedPlugin plugin)
        => _plugins[plugin.Manifest.Id] = plugin;

    /// <summary>
    /// Attempts to retrieve a loaded plugin by its identifier.
    /// </summary>
    /// <param name="id">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns>The <see cref="LoadedPlugin"/>, or <see langword="null"/> if not found.</returns>
    public LoadedPlugin? Find(string id)
        => _plugins.GetValueOrDefault(id);

    /// <summary>
    /// Returns the loaded plugins whose manifest declares <paramref name="type"/>, ordered by plugin id so every
    /// caller sees them in the same order, whatever order the plugin folders were read in.
    /// </summary>
    /// <param name="type">The plugin type the manifest must declare.</param>
    /// <returns>The matching plugins; empty when none is loaded.</returns>
    public IReadOnlyList<LoadedPlugin> OfType(PluginType type)
        => _plugins.Values
            .Where(plugin => plugin.Manifest.Type == type)
            .OrderBy(plugin => plugin.Manifest.Id, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc/>
    public bool IsLoaded(string pluginId)
        => _plugins.ContainsKey(pluginId);

    /// <inheritdoc/>
    public IReadOnlyCollection<PluginManifest> GetLoadedManifests()
        => _plugins.Values.Select(p => p.Manifest).ToList();
}
