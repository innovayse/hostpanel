namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for one version in a plugin page or in a plugin's version list.</summary>
/// <param name="Version">The version.</param>
/// <param name="SdkVersion">The SDK version it targets.</param>
/// <param name="SizeBytes">The package size in bytes.</param>
/// <param name="PublishedAt">When it was published.</param>
/// <param name="Status">The status name.</param>
/// <param name="StatusReason">Why it is in that status.</param>
/// <param name="ReplacementVersion">The version that replaces it.</param>
/// <param name="MinHostVersion">The oldest host version it runs on.</param>
/// <param name="MaxHostVersion">The newest host version it runs on.</param>
/// <param name="Changelog">The changelog text.</param>
/// <param name="Permissions">The declared capability names.</param>
/// <param name="Icon">The icon.</param>
/// <param name="Screenshots">The screenshots.</param>
internal sealed record PluginRegistryVersionWire(
    string? Version,
    string? SdkVersion,
    long SizeBytes,
    DateTimeOffset PublishedAt,
    string? Status,
    string? StatusReason,
    string? ReplacementVersion,
    string? MinHostVersion,
    string? MaxHostVersion,
    string? Changelog,
    List<string?>? Permissions,
    PluginRegistryImageWire? Icon,
    List<PluginRegistryImageWire?>? Screenshots);
