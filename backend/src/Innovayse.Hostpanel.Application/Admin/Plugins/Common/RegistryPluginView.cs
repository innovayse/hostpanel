namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>What this host is shown of one registry plugin: its page, the licence key it was read with and its versions.</summary>
/// <param name="Details">The plugin's page as the registry shows it to this host (with the stored licence key).</param>
/// <param name="Licence">The licence key stored for the plugin; <see langword="null"/> when none is stored.</param>
/// <param name="Versions">Every version the registry lists to this host, from the list a host only plugin gives a verified installation.</param>
public sealed record RegistryPluginView(RegistryPluginDetails Details, string? Licence, IReadOnlyList<RegistryPluginVersion> Versions);
