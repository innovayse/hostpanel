namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for one plugin in a listing. Every member is checked before it becomes an application type.</summary>
/// <param name="Id">The plugin id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short description.</param>
/// <param name="Author">The publisher's name.</param>
/// <param name="IconUrl">The icon address.</param>
/// <param name="Category">The category name.</param>
/// <param name="LatestVersion">The newest version.</param>
/// <param name="SdkVersion">The SDK version the newest version targets.</param>
/// <param name="Verified">Whether the registry vouches for the publisher.</param>
/// <param name="Pricing">The pricing name.</param>
internal sealed record PluginRegistryEntryWire(
    string? Id,
    string? Name,
    string? Description,
    string? Author,
    string? IconUrl,
    string? Category,
    string? LatestVersion,
    string? SdkVersion,
    bool Verified,
    string? Pricing);
