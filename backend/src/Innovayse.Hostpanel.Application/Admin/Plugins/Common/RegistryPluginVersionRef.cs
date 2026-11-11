using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Names one version of one plugin in the registry.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Version">The version, a strict <c>major.minor.patch</c>.</param>
/// <param name="Distribution">Where the plugin is distributed from; a host only plugin is looked up with a signed request and no licence key.</param>
public sealed record RegistryPluginVersionRef(string PluginId, string Version, PluginRegistryDistribution Distribution = PluginRegistryDistribution.Download);
