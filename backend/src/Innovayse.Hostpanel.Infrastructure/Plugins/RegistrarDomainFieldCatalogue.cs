using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Answers the manifest of an installed registrar plugin from the plugins the loader read: no plugin code runs, so a screen
/// can ask for the extra domain fields on every step of an order.
/// </summary>
public sealed class RegistrarDomainFieldCatalogue : IRegistrarDomainFieldCatalogue
{
    /// <summary>The plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="RegistrarDomainFieldCatalogue"/> class.</summary>
    /// <param name="registry">The plugins loaded at startup.</param>
    public RegistrarDomainFieldCatalogue(PluginRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public PluginManifest? ManifestOf(string? module)
    {
        return module is null ? null : _registry.FindRegistrar(module)?.Manifest;
    }
}
