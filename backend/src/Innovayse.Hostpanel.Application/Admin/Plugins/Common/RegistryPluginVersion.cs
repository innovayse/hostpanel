using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One published version of a plugin as the registry describes it. All text is cleaned and capped.</summary>
/// <param name="Version">The version, a strict <c>major.minor.patch</c>.</param>
/// <param name="SdkVersion">The SDK version the version targets; empty when not stated.</param>
/// <param name="SizeBytes">The package size in bytes; 0 when not stated.</param>
/// <param name="PublishedAt">When the version was published.</param>
/// <param name="Status">Where the version stands in the registry.</param>
/// <param name="StatusReason">Why the version is in that state, when stated.</param>
/// <param name="ReplacementVersion">The version that replaces a yanked or deprecated one, when stated.</param>
/// <param name="MinHostVersion">The oldest host version it runs on, when stated.</param>
/// <param name="MaxHostVersion">The newest host version it runs on, when stated.</param>
/// <param name="Changelog">The changelog text with line feed breaks, shown as plain text.</param>
/// <param name="Permissions">The sensitive capabilities the version declares.</param>
/// <param name="Icon">The icon, or <see langword="null"/> when there is none or it is not on the registry's origin.</param>
/// <param name="Screenshots">The screenshots on the registry's origin, in the registry's order.</param>
public sealed record RegistryPluginVersion(
    string Version,
    string SdkVersion,
    long SizeBytes,
    DateTimeOffset PublishedAt,
    PluginRegistryVersionStatus Status,
    string? StatusReason,
    string? ReplacementVersion,
    string? MinHostVersion,
    string? MaxHostVersion,
    string? Changelog,
    IReadOnlyList<PluginRegistryCapability> Permissions,
    RegistryPluginImage? Icon,
    IReadOnlyList<RegistryPluginImage> Screenshots);
