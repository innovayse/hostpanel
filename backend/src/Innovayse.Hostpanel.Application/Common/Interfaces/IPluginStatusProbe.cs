using Innovayse.Hostpanel.Application.Admin.Integrations.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Asks a loaded plugin how well it works, through the SDK's optional <c>IPluginStatus</c>. The adapter applies the
/// host's time limit and the activation gates (switched on, configured, no pending migration) and fails closed: a throw,
/// a malformed answer or no answer in time is a result, never an exception.
/// </summary>
public interface IPluginStatusProbe
{
    /// <summary>Tells, without running plugin code, whether the plugin is loaded and implements <c>IPluginStatus</c>.</summary>
    /// <param name="pluginId">The plugin id from its manifest.</param>
    /// <returns><see langword="true"/> when the plugin can be asked.</returns>
    public bool Supports(string pluginId);

    /// <summary>Asks the plugin once, bounded by the host's status time limit.</summary>
    /// <param name="pluginId">The plugin id from its manifest.</param>
    /// <param name="ct">The caller's cancellation; only it propagates, as an <see cref="OperationCanceledException"/>.</param>
    /// <returns>What the check found.</returns>
    public Task<PluginStatusProbeResult> ProbeAsync(string pluginId, CancellationToken ct);
}
