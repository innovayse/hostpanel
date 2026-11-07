namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for one published version of a plugin.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Version">The version.</param>
/// <param name="DownloadUrl">Where the package is served, absolute or relative to the registry address.</param>
/// <param name="Sha256">The published SHA-256 of the package, in hex.</param>
/// <param name="Verified">Whether the registry vouches for the publisher.</param>
internal sealed record PluginRegistryReleaseWire(string? PluginId, string? Version, string? DownloadUrl, string? Sha256, bool Verified);
