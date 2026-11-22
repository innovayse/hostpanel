using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The control panel modules a server can be given: <see cref="ServerModules.Known"/> plus the <c>serverModules</c> of every loaded
/// provisioning plugin that declares an operation on a provisioned server (a panel today, a virtual server platform next). A module
/// the host knows and a plugin declares is listed once, in the host's spelling and under the plugin's name, so a server saved
/// before the panel moved to a plugin keeps matching. The one reader of the lists on the admin side; calls go through
/// <see cref="ServerModulePluginResolver"/>.
/// </summary>
public sealed class ServerModuleCatalog : IServerModuleCatalog
{
    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ServerModuleCatalog"/> class.</summary>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    public ServerModuleCatalog(PluginRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ServerModuleOptionDto> List()
    {
        // Insertion order is the list order: the host's modules first, then the plugins' in plugin id order.
        var options = new Dictionary<string, ServerModuleOptionDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ServerModules.Known)
        {
            options[id] = new ServerModuleOptionDto(id, id, ServerModuleSource.BuiltIn);
        }

        foreach (var plugin in _registry.OfType(PluginType.Provisioning).Where(p => PluginCapabilityRules.DeclaresProvisioning(p.Manifest)))
        {
            foreach (var id in plugin.Manifest.ServerModules)
            {
                var spelling = options.TryGetValue(id, out var known) ? known.Id : id;
                options[id] = new ServerModuleOptionDto(spelling, plugin.Manifest.Name, ServerModuleSource.Plugin);
            }
        }

        return [.. options.Values];
    }

    /// <inheritdoc/>
    public string? Canonical(string module, string? stored = null)
    {
        return List().FirstOrDefault(option => ServerModules.Same(option.Id, module))?.Id
            ?? (stored is not null && ServerModules.Same(stored, module) ? stored : null);
    }
}
