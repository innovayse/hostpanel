namespace Innovayse.Infrastructure.Plugins;
using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.SDK.Plugins;

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

    /// <inheritdoc/>
    public bool IsLoaded(string pluginId)
        => _plugins.ContainsKey(pluginId);

    /// <inheritdoc/>
    public IReadOnlyCollection<PluginManifest> GetLoadedManifests()
        => _plugins.Values.Select(p => p.Manifest).ToList();
}

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType);
