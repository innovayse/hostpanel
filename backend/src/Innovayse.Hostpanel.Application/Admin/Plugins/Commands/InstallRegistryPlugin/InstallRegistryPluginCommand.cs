using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallRegistryPlugin;

/// <summary>
/// Installs one version of a plugin from the plugin registry, or stages it as an upgrade when the plugin is already
/// installed. The package is downloaded from the registry, checked against the checksum the registry published, held to
/// the operator's registry trust setting and then installed like an upload, with its signature always enforced.
/// </summary>
/// <param name="PluginId">The plugin id; set from the route.</param>
/// <param name="Version">The semantic version to install.</param>
/// <param name="AllowDowngrade">
/// Whether the operator accepts a version older than the installed one. Without it an older version is refused with
/// <c>PLUGIN_DOWNGRADE_REFUSED</c>.
/// </param>
public sealed record InstallRegistryPluginCommand(string PluginId, string Version, bool AllowDowngrade = false) : IDemoRestricted;
