using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Builds the call-bound services of one plugin call: the read-only host facts and the plugin's own store, both
/// bound to the call's client by the host so no method on them takes a client id.
/// </summary>
public interface IPluginCallServices
{
    /// <summary>Builds the services for one call.</summary>
    /// <param name="binding">The plugin being called and the client the call acts for.</param>
    /// <returns>The services, valid for the call.</returns>
    PluginCallServices Create(PluginCallBinding binding);
}
