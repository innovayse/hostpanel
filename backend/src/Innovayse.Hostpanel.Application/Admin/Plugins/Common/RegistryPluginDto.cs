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
    bool UpdateAvailable = false);
