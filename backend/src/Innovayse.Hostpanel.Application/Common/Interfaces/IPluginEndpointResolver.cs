using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Finds the app plugin a plugin route belongs to and hands out its endpoints, in two steps, so the host can
/// authorise the caller before any plugin code runs. Implemented in Infrastructure with the same activation
/// gates as every other plugin type.
/// </summary>
public interface IPluginEndpointResolver
{
    /// <summary>Finds the manifest of a loaded plugin that answers routes.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <returns>
    /// The manifest, or <see langword="null"/> when no plugin of that id is loaded or the plugin is not an app
    /// plugin, which are the same answer on purpose.
    /// </returns>
    PluginManifest? Find(string pluginId);

    /// <summary>
    /// Checks that the plugin is switched on and configured, without building it, and returns the way to build it.
    /// Nothing of the plugin runs here, not even its constructor.
    /// </summary>
    /// <param name="pluginId">The id of a plugin <see cref="Find"/> returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A function that builds the plugin's endpoints, or <see langword="null"/> when the plugin is disabled, lacks a
    /// required setting or does not implement <see cref="IPluginEndpoints"/>. Building can throw what the plugin's
    /// own constructor throws.
    /// </returns>
    Task<Func<IPluginEndpoints>?> PrepareAsync(string pluginId, CancellationToken ct);
}
