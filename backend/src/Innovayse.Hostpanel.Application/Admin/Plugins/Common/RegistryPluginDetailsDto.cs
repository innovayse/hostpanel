using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The details page of one registry plugin (<c>GET /api/admin/plugins/registry/{id}</c>): the registry's page for it, set
/// beside what is installed on this server and whether a licence key is stored for it. The key itself is never returned.
/// </summary>
/// <param name="Id">The plugin id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Author">The publisher's name.</param>
/// <param name="Description">The description.</param>
/// <param name="IconUrl">An https address of the icon on the registry's host; <see langword="null"/> when none.</param>
/// <param name="Category">The registry category.</param>
/// <param name="Pricing">Whether the plugin is free, paid or private.</param>
/// <param name="Verified">Whether the registry vouches for the publisher.</param>
/// <param name="LatestVersion">The newest published version; <see langword="null"/> when nothing is published.</param>
/// <param name="Installed">Whether a plugin with this id is installed on this server.</param>
/// <param name="InstalledVersion">The installed version; <see langword="null"/> when not installed.</param>
/// <param name="StagedVersion">The version waiting for the restart; <see langword="null"/> when none.</param>
/// <param name="UpdateAvailable">Whether <paramref name="LatestVersion"/> is newer than the installed and the staged one.</param>
/// <param name="RequiresLicence">Whether the plugin is paid or private, so the registry asks for a licence key.</param>
/// <param name="HasLicenceKey">Whether a licence key is stored on this server for the plugin.</param>
/// <param name="BuyUrl">Where to buy a paid plugin; <see langword="null"/> when not stated.</param>
/// <param name="DocumentationUrl">The documentation address; <see langword="null"/> when not stated.</param>
/// <param name="SupportUrl">The support address; <see langword="null"/> when not stated.</param>
/// <param name="RepositoryUrl">The source repository address; <see langword="null"/> when not stated.</param>
/// <param name="Licence">The licence name; <see langword="null"/> when not stated.</param>
/// <param name="Versions">Every version the registry shows, newest first.</param>
public sealed record RegistryPluginDetailsDto(
    string Id,
    string Name,
    string Author,
    string Description,
    string? IconUrl,
    PluginRegistryCategory Category,
    PluginRegistryPricing Pricing,
    bool Verified,
    string? LatestVersion,
    bool Installed,
    string? InstalledVersion,
    string? StagedVersion,
    bool UpdateAvailable,
    bool RequiresLicence,
    bool HasLicenceKey,
    string? BuyUrl,
    string? DocumentationUrl,
    string? SupportUrl,
    string? RepositoryUrl,
    string? Licence,
    IReadOnlyList<RegistryPluginVersionDto> Versions);
