using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Names the panel plugin that serves a server's module: <see cref="IServerModulePluginResolver"/> chooses it for the
/// <c>provisioningPanel</c> capability, and the registry gives its display name and whether it ships an icon.
/// </summary>
public sealed class ServerPanelPluginReader : IServerPanelPluginReader
{
    /// <summary>Chooses the plugin that serves a module.</summary>
    private readonly IServerModulePluginResolver _resolver;

    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ServerPanelPluginReader"/> class.</summary>
    /// <param name="resolver">Chooses the plugin that serves a module.</param>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    public ServerPanelPluginReader(IServerModulePluginResolver resolver, PluginRegistry registry)
    {
        _resolver = resolver;
        _registry = registry;
    }

    /// <inheritdoc/>
    public ServicePanelPluginDto? Find(string module)
    {
        var match = _resolver.Find(module, PluginCapabilities.ProvisioningPanel);
        if (match.Outcome != ServerModulePluginOutcome.Served || _registry.Find(match.PluginId!) is not { } plugin)
        {
            return null;
        }

        return new ServicePanelPluginDto(
            plugin.Manifest.Id,
            plugin.Manifest.Name,
            plugin.Icon is not null ? plugin.Manifest.Version : null);
    }
}
