using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Applies a plugin change inside the running process. A plugin's assemblies live in a collectible
/// <see cref="PluginLoadContext"/>, so an upgrade is applied by letting the calls that still run in the old version finish,
/// unloading its context, swapping the staged files in and loading them in a context of their own; a removal ends with the
/// unload.
/// </summary>
/// <remarks>
/// <para>
/// <b>Drain.</b> While the old version drains, the plugin is held behind the same gate a pending data migration uses
/// (<see cref="PluginRegistry.MarkMigrationPending"/>), so no new call starts in it, and the reload waits at most
/// <see cref="Settings.PluginOptions.DrainSeconds"/> for the instances already built to be released
/// (<see cref="PluginUsage"/>). A drain that does not finish leaves everything as it was and the staged version waits for
/// a restart, so a running call is never cut off.
/// </para>
/// <para>
/// <b>What cannot be reloaded.</b> The host registers per plugin id once at startup (the plugin's named HTTP client), starts
/// hosted plugins once and reads a plugin's type and endpoints capability when it builds its routes, so a plugin that was not
/// loaded, a hosted plugin and a version that changes the plugin type or the endpoints capability are left to the restart.
/// The upgrade then needs the migration hook, which runs as it does at startup (<see cref="IPluginMigrationRetry"/>).
/// </para>
/// <para>
/// One change is applied at a time. The unload itself is cooperative: the runtime frees the old assemblies once nothing
/// refers to a type or an instance of them, which is why the host keeps no plugin type outside the registry.
/// </para>
/// </remarks>
internal sealed class PluginHotReloader : IPluginHotReloader
{
    /// <summary>The registry the plugin is swapped in.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>The registry the plugin's integration entry is kept in.</summary>
    private readonly IPluginIntegrationRegistry _integrations;

    /// <summary>Checks and loads the folder of the new version.</summary>
    private readonly PluginFolderLoader _loader;

    /// <summary>Tells how the swapped-in version's load ended.</summary>
    private readonly PluginChangeStore _changes;

    /// <summary>Runs the migration hook of the swapped-in version.</summary>
    private readonly IPluginMigrationRetry _migrations;

    /// <summary>Drops the panels' remembered plugin availability once the plugin changed.</summary>
    private readonly IPluginAvailabilityInvalidator _availability;

    /// <summary>The upload folder, where staged versions wait and installed ones live.</summary>
    private readonly string _installRoot;

    /// <summary>The longest the reload waits for the running calls of the old version.</summary>
    private readonly TimeSpan _drain;

    /// <summary>Reads whether the admin setting "allow unsigned plugins" is on right now.</summary>
    private readonly Func<bool> _allowUnsigned;

    /// <summary>Records each reload and why one was left to the restart.</summary>
    private readonly ILogger<PluginHotReloader> _logger;

    /// <summary>Lets one change run at a time.</summary>
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>Initializes the reloader.</summary>
    /// <param name="registry">The registry the plugin is swapped in.</param>
    /// <param name="integrations">The registry the plugin's integration entry is kept in.</param>
    /// <param name="loader">Checks and loads the folder of the new version.</param>
    /// <param name="changes">Tells how the swapped-in version's load ended.</param>
    /// <param name="migrations">Runs the migration hook of the swapped-in version.</param>
    /// <param name="availability">Drops the panels' remembered plugin availability once the plugin changed.</param>
    /// <param name="installRoot">The upload folder, where staged versions wait and installed ones live.</param>
    /// <param name="drain">The longest the reload waits for the running calls of the old version.</param>
    /// <param name="allowUnsigned">Reads whether the admin setting "allow unsigned plugins" is on right now.</param>
    /// <param name="logger">Records each reload and why one was left to the restart.</param>
    public PluginHotReloader(
        PluginRegistry registry,
        IPluginIntegrationRegistry integrations,
        PluginFolderLoader loader,
        PluginChangeStore changes,
        IPluginMigrationRetry migrations,
        IPluginAvailabilityInvalidator availability,
        string installRoot,
        TimeSpan drain,
        Func<bool> allowUnsigned,
        ILogger<PluginHotReloader> logger)
    {
        _registry = registry;
        _integrations = integrations;
        _loader = loader;
        _changes = changes;
        _migrations = migrations;
        _availability = availability;
        _installRoot = installRoot;
        _drain = drain;
        _allowUnsigned = allowUnsigned;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PluginReloadOutcome> ReloadAsync(string pluginId, CancellationToken ct)
    {
        await _one.WaitAsync(ct);
        try
        {
            var current = _registry.Find(pluginId);
            var next = _loader.ReadManifest(Path.Combine(_installRoot, PluginFolders.Staged, pluginId));
            if (current is null || next is null || !CanBeReplacedBy(current, next))
            {
                _logger.LogInformation("Plugin {Id} cannot be reloaded in the running process, the staged version waits for the restart", pluginId);
                return PluginReloadOutcome.NeedsRestart;
            }

            if (!await DrainAsync(current))
            {
                return PluginReloadOutcome.NeedsRestart;
            }

            return await SwapAsync(current);
        }
        finally
        {
            _one.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<PluginReloadOutcome> UnloadAsync(string pluginId, CancellationToken ct)
    {
        await _one.WaitAsync(ct);
        try
        {
            var current = _registry.Find(pluginId);
            if (current is null)
            {
                // Nothing of it is in memory (it was refused, or never loaded), so there is nothing to unload.
                _registry.ClearRefused(pluginId);
                _availability.Invalidate();
                return PluginReloadOutcome.Applied;
            }

            if (current.IsHosted)
            {
                _logger.LogInformation("Plugin {Id} cannot be unloaded in the running process, it goes at the restart", pluginId);
                return PluginReloadOutcome.NeedsRestart;
            }

            if (!await DrainAsync(current))
            {
                return PluginReloadOutcome.NeedsRestart;
            }

            _registry.ClearMigrationPending(pluginId);
            Release(current);
            _logger.LogInformation("Plugin {Id} was unloaded", pluginId);
            return PluginReloadOutcome.Applied;
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>
    /// Whether a staged version can take the place of the running one inside the process: the plugin type and the endpoints
    /// capability stay as they are, and neither version is a hosted plugin.
    /// </summary>
    /// <param name="current">The loaded plugin.</param>
    /// <param name="next">The manifest of the staged version.</param>
    /// <returns><see langword="true"/> when the staged version can be swapped in without a restart.</returns>
    private static bool CanBeReplacedBy(LoadedPlugin current, PluginManifest next)
    {
        return !current.IsHosted
            && next.Type != PluginType.Hosted
            && current.Manifest.Type == next.Type
            && current.Manifest.ServesEndpoints() == next.ServesEndpoints();
    }

    /// <summary>
    /// Closes the plugin to new calls and waits for the instances already built to be released. When the wait runs out, or
    /// ends in an error, the gate is lifted again, so the plugin goes on exactly as before.
    /// </summary>
    /// <remarks>
    /// The wait is not tied to the caller's request: a request that is cancelled while the plugin is closed would otherwise
    /// end the wait halfway, and the drain is bounded by <see cref="Settings.PluginOptions.DrainSeconds"/> anyway.
    /// </remarks>
    /// <param name="current">The plugin to drain.</param>
    /// <returns><see langword="true"/> when no call runs in the plugin any more and the gate is closed.</returns>
    private async Task<bool> DrainAsync(LoadedPlugin current)
    {
        var id = current.Manifest.Id;
        var alreadyPending = _registry.IsMigrationPending(id);
        _registry.MarkMigrationPending(id);

        var idle = false;
        try
        {
            idle = await current.Usage.DrainAsync(_drain);
        }
        finally
        {
            if (!idle && !alreadyPending)
            {
                _registry.ClearMigrationPending(id);
            }
        }

        if (!idle)
        {
            _logger.LogWarning(
                "Plugin {Id} still had running calls after {Seconds} s, the change waits for the restart", id, _drain.TotalSeconds);
        }

        return idle;
    }

    /// <summary>
    /// Unloads the drained version, swaps the staged files in, loads them and runs the migration hook.
    /// </summary>
    /// <remarks>
    /// Once the old version is released the swap runs to its end whatever happens to the caller's request, and the gate is
    /// settled on every exit: the migration hook lifts it unless the new version has a migration still to run, so an error
    /// in the swap or the load never leaves the plugin closed for good.
    /// </remarks>
    /// <param name="current">The drained plugin, which is released here.</param>
    /// <returns><see cref="PluginReloadOutcome.Applied"/> when the new version is loaded and is not a plugin the host would start at startup.</returns>
    private async Task<PluginReloadOutcome> SwapAsync(LoadedPlugin current)
    {
        var id = current.Manifest.Id;
        bool swapped;
        LoadedPlugin? loaded;
        try
        {
            Release(current);

            // The old assemblies are no longer in use, so the installed folder can be replaced. A swap that fails leaves the
            // installed version in place and the load below brings that version back.
            swapped = PluginUpgradeApplier.ApplyOne(_installRoot, id, _logger);
            _registry.ClearRefused(id);

            // The caller's own request writes the activity log line, so the record is already reported.
            loaded = _loader.LoadChanged(
                Path.Combine(_installRoot, id), PluginOrigin.Uploaded, new HashSet<string>(StringComparer.OrdinalIgnoreCase), _allowUnsigned(), reported: true);
            _availability.Invalidate();
        }
        finally
        {
            // Runs the new version's migration, or lifts the drain gate when none is pending (a restored version has none:
            // the restore dropped the record).
            await _migrations.RetryAsync(id, CancellationToken.None);
        }

        if (swapped && _changes.Find(id)?.Result == PluginChangeResult.Failed)
        {
            _logger.LogWarning("Plugin {Id} could not load its new version, the previous version runs again", id);
            return PluginReloadOutcome.Failed;
        }

        if (loaded is null || !swapped || loaded.IsHosted)
        {
            // Refused or broken stays in the registry's refusal list; a hosted plugin is started by the next start.
            return PluginReloadOutcome.NeedsRestart;
        }

        _logger.LogInformation("Plugin {Id} was reloaded as version {Version}", id, loaded.Manifest.Version);
        return PluginReloadOutcome.Applied;
    }

    /// <summary>Lets go of a plugin version: out of both registries, then its load context is unloaded.</summary>
    /// <param name="plugin">The drained plugin.</param>
    private void Release(LoadedPlugin plugin)
    {
        _registry.Remove(plugin.Manifest.Id);
        _integrations.Remove(plugin.Manifest.Id);
        _availability.Invalidate();
        plugin.LoadContext?.Unload();
    }
}
