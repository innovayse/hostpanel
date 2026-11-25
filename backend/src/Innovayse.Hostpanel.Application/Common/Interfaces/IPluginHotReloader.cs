using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Applies a plugin change inside the running process: the plugin's calls that are still running finish, its load context
/// is unloaded and, for an upgrade, the staged version is loaded in its place. A change this cannot apply is left staged
/// for a restart. Implemented in Infrastructure, where plugins are loaded.
/// </summary>
public interface IPluginHotReloader
{
    /// <summary>
    /// Swaps the staged version of an installed plugin in and loads it, without a restart. Only a plugin that is loaded
    /// now, keeps its plugin type and its endpoints capability and is not hosted can be reloaded; anything else, and a
    /// drain that does not finish in time, leaves the staged version for the restart.
    /// </summary>
    /// <param name="pluginId">The id of the plugin that has a staged version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="PluginReloadOutcome.Applied"/> when the new version is loaded; <see cref="PluginReloadOutcome.Failed"/> when it could not be loaded and the previous version was restored; otherwise <see cref="PluginReloadOutcome.NeedsRestart"/>.</returns>
    public Task<PluginReloadOutcome> ReloadAsync(string pluginId, CancellationToken ct);

    /// <summary>
    /// Unloads a plugin whose package was just removed, once its running calls have finished. A plugin that is not loaded
    /// has nothing in memory to unload. One that is hosted or does not drain in time stays in memory until the restart.
    /// </summary>
    /// <param name="pluginId">The id of the removed plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="PluginReloadOutcome.Applied"/> when the plugin is gone from the process (or never was in it); otherwise <see cref="PluginReloadOutcome.NeedsRestart"/>.</returns>
    public Task<PluginReloadOutcome> UnloadAsync(string pluginId, CancellationToken ct);
}
