using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// <see cref="IPluginCallServices"/> for a host that does not share client facts or keep plugin data yet: the host
/// facts and the store of every call say so by throwing (see <see cref="UnavailablePluginHostApi"/> and
/// <see cref="UnavailablePluginStore"/>). The seam is the port: the host API and the plugin store replace this one
/// registration and no plugin, use case or controller changes.
/// </summary>
public sealed class UnavailablePluginCallServices : IPluginCallServices
{
    /// <inheritdoc/>
    public PluginCallServices Create(PluginCallBinding binding)
    {
        return new PluginCallServices(new UnavailablePluginHostApi(), new UnavailablePluginStore());
    }
}
