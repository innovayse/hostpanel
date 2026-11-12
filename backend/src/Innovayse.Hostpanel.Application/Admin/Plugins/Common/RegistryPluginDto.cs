using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// One plugin of the registry browser (<c>GET /api/admin/plugins/registry</c>) and of the update list
/// (<c>GET /api/admin/plugins/registry/updates</c>). The registry client fills the first ten members from the registry's
/// reply (text already cleaned and capped); the handler then sets the last three from what is installed on this server.
/// </summary>
/// <param name="Id">The plugin id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Author">The publisher's name.</param>
/// <param name="Description">A short description.</param>
/// <param name="IconUrl">An https address of the icon on the registry's host; <see langword="null"/> when none.</param>
/// <param name="Category">The registry category.</param>
/// <param name="LatestVersion">The newest version the registry publishes.</param>
/// <param name="SdkVersion">The plugin SDK version the newest version targets.</param>
/// <param name="Verified">Whether the registry vouches for the publisher.</param>
/// <param name="Pricing">Whether the plugin is free, paid or private.</param>
/// <param name="Installed">Whether a plugin with this id is installed on this server.</param>
/// <param name="InstalledVersion">The installed version; <see langword="null"/> when not installed.</param>
/// <param name="UpdateAvailable">Whether <paramref name="LatestVersion"/> is newer than the installed one and not already staged.</param>
/// <param name="Distribution">Where the package can be obtained from; a host only plugin is asked for with a signed request.</param>
/// <param name="PublisherKind">Whether a person or an organization publishes the plugin; <see langword="null"/> when the registry does not say.</param>
/// <param name="Tags">The plugin's tags, cleaned and capped; <see langword="null"/> when the source carries none (the update list).</param>
/// <param name="Downloads">Downloads of every version added up; <see langword="null"/> when the registry does not say.</param>
/// <param name="LatestChangelog">The release notes of <paramref name="LatestVersion"/> as Markdown text, cleaned and capped; <see langword="null"/> when the registry sends none.</param>
/// <param name="Rating">The reviews summary; <see langword="null"/> when the plugin has no reviews or the registry sends no usable summary.</param>
public sealed record RegistryPluginDto(
    string Id,
    string Name,
    string Author,
    string Description,
    string? IconUrl,
    PluginRegistryCategory Category,
    string LatestVersion,
    string SdkVersion,
    bool Verified,
    PluginRegistryPricing Pricing,
    bool Installed = false,
    string? InstalledVersion = null,
    bool UpdateAvailable = false,
    PluginRegistryDistribution Distribution = PluginRegistryDistribution.Download,
    PluginRegistryPublisherKind? PublisherKind = null,
    IReadOnlyList<string>? Tags = null,
    long? Downloads = null,
    string? LatestChangelog = null,
    RegistryPluginRatingDto? Rating = null);
