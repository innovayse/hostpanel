using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads the manifest of an installed registrar plugin, the one source of the extra domain fields a registry asks for. No
/// plugin code runs: the fields are data in <see cref="PluginManifest.DomainFields"/>, so the host holds nothing that is
/// specific to a zone or a country.
/// </summary>
public interface IRegistrarDomainFieldCatalogue
{
    /// <summary>Finds the manifest of the installed registrar plugin a stored module value names.</summary>
    /// <param name="module">The module value, compared with the plugin ids case-insensitively; <see langword="null"/> for none.</param>
    /// <returns>The manifest, or <see langword="null"/> when no installed registrar plugin has the module.</returns>
    public PluginManifest? ManifestOf(string? module);
}
