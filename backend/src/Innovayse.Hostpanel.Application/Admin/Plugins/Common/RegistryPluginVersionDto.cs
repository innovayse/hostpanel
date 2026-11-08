using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One published version of a registry plugin on the details page (<c>GET /api/admin/plugins/registry/{id}</c>).</summary>
/// <param name="Version">The version, a strict <c>major.minor.patch</c>.</param>
/// <param name="PublishedAt">When the registry published it.</param>
/// <param name="Status">Where the version stands in the registry; only <c>Published</c> and <c>Deprecated</c> can be installed.</param>
/// <param name="StatusReason">Why the version is in that state; <see langword="null"/> when the registry gives none.</param>
/// <param name="ReplacementVersion">The version that replaces a yanked or deprecated one; <see langword="null"/> when none.</param>
/// <param name="Changelog">The release notes as plain text; <see langword="null"/> when none.</param>
/// <param name="MinHostVersion">The oldest Hostpanel version it runs on; <see langword="null"/> for no lower bound.</param>
/// <param name="MaxHostVersion">The newest Hostpanel version it runs on; <see langword="null"/> for no upper bound.</param>
/// <param name="Compatible">Whether it runs on the Hostpanel version this server runs.</param>
/// <param name="Permissions">The sensitive capabilities the version declares.</param>
/// <param name="Screenshots">The screenshots, on the registry's own origin.</param>
public sealed record RegistryPluginVersionDto(
    string Version,
    DateTimeOffset PublishedAt,
    PluginRegistryVersionStatus Status,
    string? StatusReason,
    string? ReplacementVersion,
    string? Changelog,
    string? MinHostVersion,
    string? MaxHostVersion,
    bool Compatible,
    IReadOnlyList<PluginRegistryCapability> Permissions,
    IReadOnlyList<RegistryPluginImage> Screenshots);
