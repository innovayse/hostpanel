using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Finds the installed plugin that serves a server module for one capability: the loaded provisioning plugins whose manifest
/// declares the capability and lists the module in <c>serverModules</c>, matched ignoring case. Two candidates are a conflict and
/// none is chosen, as <see cref="IdentityPluginResolver"/> does for identity plugins. It never looks at whether a plugin is switched
/// on: an installed plugin that owns a module serves it, and a call that finds it off answers that it is unavailable, so a half
/// finished migration from a built in provider is never hidden by a silent fall back.
/// </summary>
public sealed class ServerModulePluginResolver : IServerModulePluginResolver
{
    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Records which plugins collided over a module.</summary>
    private readonly ILogger<ServerModulePluginResolver> _logger;

    /// <summary>Initializes a new instance of the <see cref="ServerModulePluginResolver"/> class.</summary>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    /// <param name="logger">Records which plugins collided over a module.</param>
    public ServerModulePluginResolver(PluginRegistry registry, ILogger<ServerModulePluginResolver> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ServerModulePluginMatch Find(string module, string capability)
    {
        var candidates = _registry.OfType(PluginType.Provisioning)
            .Where(plugin => plugin.Manifest.Declares(capability)
                && plugin.Manifest.ServerModules.Any(declared => ServerModules.Same(declared, module)))
            .Select(plugin => plugin.Manifest.Id)
            .ToList();

        if (candidates.Count > 1)
        {
            _logger.LogError(
                "Plugins {PluginIds} all serve the module {Module} for {Capability}; none is chosen.",
                string.Join(", ", candidates), module, capability);
            return new ServerModulePluginMatch(ServerModulePluginOutcome.Conflict, null);
        }

        return candidates.Count == 1
            ? new ServerModulePluginMatch(ServerModulePluginOutcome.Served, candidates[0])
            : new ServerModulePluginMatch(ServerModulePluginOutcome.NotServed, null);
    }
}
