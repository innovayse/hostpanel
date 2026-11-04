namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Runs the migration hook of a plugin whose upgrade was swapped in but whose hook has not succeeded yet. The host tries
/// every such plugin at startup; this port is the retry for the moment an operator switches the plugin on or saves its
/// settings, which is when a plugin that was switched off or lacked a required setting becomes able to run its hook.
/// Implemented in Infrastructure, where plugins are activated.
/// </summary>
public interface IPluginMigrationRetry
{
    /// <summary>
    /// Runs the plugin's pending migration, if it has one, with the hook's time limit. A plugin with nothing pending is
    /// left alone. A hook that fails, times out or cannot run yet is logged and leaves the plugin gated; it is never
    /// thrown to the caller.
    /// </summary>
    /// <param name="pluginId">The plugin whose settings were just saved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the attempt ended.</returns>
    Task RetryAsync(string pluginId, CancellationToken ct);
}
