using Hostpanel.Sdk.Plugins.Endpoints;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The dependency scope of one streamed plugin call: the plugin, its host API and its store are all built from it, on its
/// own data context, so they stay usable until the scope is disposed, after the stream ended, failed or the client left.
/// </summary>
public interface IPluginCallScope : IAsyncDisposable
{
    /// <summary>
    /// Checks again, inside this scope, that the plugin is switched on and configured, and returns the way to build it from
    /// this scope. Nothing of the plugin runs here.
    /// </summary>
    /// <param name="pluginId">The id of the plugin the gate admitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A function that builds the plugin's endpoints from this scope, or <see langword="null"/> when the plugin was switched
    /// off or lost a required setting since the gate admitted the call.
    /// </returns>
    public Task<Func<IPluginEndpoints>?> PrepareEndpointsAsync(string pluginId, CancellationToken ct);

    /// <summary>Builds the host API and the store of the call on this scope's data context.</summary>
    /// <param name="binding">The plugin being called and the client the call acts for.</param>
    /// <returns>The services, valid until this scope is disposed.</returns>
    public PluginCallServices CreateServices(PluginCallBinding binding);
}
