using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One published version of a plugin: where its package is and what the registry says it must hash to.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Version">The version.</param>
/// <param name="DownloadUrl">Where the ZIP package is served from; the registry client only fetches it from the registry's own host.</param>
/// <param name="Sha256">The lower-case hex SHA-256 the registry published for the package.</param>
/// <param name="Verified">Whether the registry vouches for the publisher of this plugin.</param>
/// <param name="Distribution">Where the plugin is distributed from; a host only package is fetched with a download ticket and signed requests.</param>
public sealed record RegistryPluginRelease(string PluginId, string Version, Uri DownloadUrl, string Sha256, bool Verified, PluginRegistryDistribution Distribution = PluginRegistryDistribution.Download);
