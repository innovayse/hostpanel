using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The plugins whose pages the panels may show, with their manifests and view documents. A plugin is available exactly
/// when its routes answer: a loaded app plugin, switched on and configured. The answer for every plugin is computed in
/// one pass and remembered for a few seconds; the use cases that write plugin settings drop it through
/// <see cref="IPluginAvailabilityInvalidator"/>, so a plugin the admin switches on or off appears in or leaves the panels
/// at once.
/// </summary>
public interface IPluginUiSource
{
    /// <summary>Lists the available plugins.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugins, ordered by id; empty when none is available.</returns>
    Task<IReadOnlyList<PluginUiPackage>> ListAvailableAsync(CancellationToken ct);

    /// <summary>Finds one available plugin.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugin, or <see langword="null"/> when it is unknown, not an app plugin, switched off or not configured, which are the same answer on purpose.</returns>
    Task<PluginUiPackage?> FindAvailableAsync(string pluginId, CancellationToken ct);
}
