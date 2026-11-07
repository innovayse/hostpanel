namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// The registry's JSON for one published version of a plugin: the answer of a release lookup, and an item of the answer
/// to the newest-version lookup.
/// </summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Version">The version.</param>
/// <param name="DownloadUrl">Where the package is served, absolute or relative to the registry address.</param>
/// <param name="Sha256">The published SHA-256 of the package, in hex.</param>
/// <param name="Verified">Whether the registry vouches for the publisher; a JSON null reads as false.</param>
/// <param name="SdkVersion">The SDK version the version targets.</param>
/// <param name="RequiresLicence">Whether the package needs a licence key; a JSON null reads as false.</param>
internal sealed record PluginRegistryReleaseWire(
    string? PluginId,
    string? Version,
    string? DownloadUrl,
    string? Sha256,
    bool? Verified,
    string? SdkVersion = null,
    bool? RequiresLicence = null);
