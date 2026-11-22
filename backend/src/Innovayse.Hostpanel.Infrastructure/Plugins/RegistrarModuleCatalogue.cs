using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Answers which registrar plugins are installed and what each offers, from the manifests the loader read: no plugin code
/// runs, so a list of domains can ask for every row.
/// </summary>
public sealed class RegistrarModuleCatalogue : IRegistrarModuleCatalogue
{
    /// <summary>What a module that names no installed registrar plugin offers.</summary>
    private static readonly RegistrarCapabilitiesDto None = new(false, false, false);

    /// <summary>The plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="RegistrarModuleCatalogue"/> class.</summary>
    /// <param name="registry">The plugins loaded at startup.</param>
    public RegistrarModuleCatalogue(PluginRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public string? FindId(string module)
    {
        return _registry.FindRegistrar(module)?.Manifest.Id;
    }

    /// <inheritdoc />
    public RegistrarCapabilitiesDto CapabilitiesOf(string? module)
    {
        var plugin = module is null ? null : _registry.FindRegistrar(module);
        if (plugin is null || !plugin.Manifest.Declares(PluginCapabilities.RegistrarOperations))
        {
            return None;
        }

        return new RegistrarCapabilitiesDto(
            plugin.Manifest.Declares(PluginCapabilities.RegistrarDnsManagement),
            plugin.Manifest.Declares(PluginCapabilities.RegistrarEmailForwarding),
            plugin.Manifest.Declares(PluginCapabilities.RegistrarWhois));
    }
}
