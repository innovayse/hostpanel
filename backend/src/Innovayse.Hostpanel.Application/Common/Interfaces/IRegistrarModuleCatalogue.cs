using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads which registrar plugins are installed and what each offers, from their manifests alone: no plugin
/// code runs, so it is safe to ask on every list and detail read.
/// </summary>
public interface IRegistrarModuleCatalogue
{
    /// <summary>Finds the installed registrar plugin a stored module value names.</summary>
    /// <param name="module">The module value, compared with the plugin ids case-insensitively.</param>
    /// <returns>The plugin id as the plugin declares it, or <see langword="null"/> when no installed registrar plugin has it.</returns>
    public string? FindId(string module);

    /// <summary>Tells which optional operations a registrar plugin offers.</summary>
    /// <param name="module">The module value, compared with the plugin ids case-insensitively.</param>
    /// <returns>The offered operations; none when the module names no installed registrar plugin.</returns>
    public RegistrarCapabilitiesDto CapabilitiesOf(string? module);
}
