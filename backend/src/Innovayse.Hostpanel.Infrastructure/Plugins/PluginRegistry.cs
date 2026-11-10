using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Tracks all plugins that were successfully loaded during application startup.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    /// <summary>Loaded plugin entries, keyed by plugin id.</summary>
    private readonly Dictionary<string, LoadedPlugin> _plugins = [];

    /// <summary>Plugins the loader skipped for a broken manifest rule, keyed by plugin id.</summary>
    private readonly Dictionary<string, RefusedPlugin> _refused = [];

    /// <summary>Plugin ids whose upgrade was swapped in and whose data migration has not succeeded yet (case-insensitive).</summary>
    private readonly HashSet<string> _migrationPending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Guards <see cref="_migrationPending"/>, which the startup runner, a settings save and every plugin call touch.</summary>
    private readonly object _pendingLock = new();

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
    /// Keeps a plugin the loader skipped for a broken manifest rule, so the admin list can show it. A refused plugin
    /// is never in <see cref="LoadedPlugins"/>.
    /// </summary>
    /// <param name="plugin">The refused plugin.</param>
    internal void RegisterRefused(RefusedPlugin plugin)
        => _refused[plugin.PluginId] = plugin;

    /// <summary>
    /// Flags a plugin as waiting for its data migration: set from the pending upgrade record at startup and again when a
    /// migration attempt fails or times out. The plugin is not activated while the flag is set.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    internal void MarkMigrationPending(string pluginId)
    {
        lock (_pendingLock)
        {
            _migrationPending.Add(pluginId);
        }
    }

    /// <summary>Clears the flag once the plugin's migration ran and was audited, or the plugin is gone.</summary>
    /// <param name="pluginId">The plugin id.</param>
    internal void ClearMigrationPending(string pluginId)
    {
        lock (_pendingLock)
        {
            _migrationPending.Remove(pluginId);
        }
    }

    /// <inheritdoc/>
    public bool IsMigrationPending(string pluginId)
    {
        lock (_pendingLock)
        {
            return _migrationPending.Contains(pluginId);
        }
    }

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

    /// <inheritdoc/>
    public PluginIconDto? GetIcon(string pluginId)
        => _plugins.GetValueOrDefault(pluginId)?.Icon;

    /// <inheritdoc/>
    public IReadOnlyCollection<RefusedPlugin> GetRefusedPlugins()
        => _refused.Values.ToList();
}
