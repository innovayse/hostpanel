using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Domain.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// One row of the admin plugin list, returned by <c>GET /api/admin/plugins</c>: an installed plugin, a plugin the host
/// refused to load, or a removed plugin whose data is still kept (see <paramref name="Status"/>).
/// </summary>
/// <param name="Id">Unique plugin identifier from <c>plugin.json</c> (the folder name for a refused or removed plugin).</param>
/// <param name="Name">Human-readable display name; the id for a refused or removed plugin whose manifest is not read.</param>
/// <param name="Version">Semver version string; empty for a removed plugin.</param>
/// <param name="Author">Author name or organisation.</param>
/// <param name="Description">Short description shown in the admin panel.</param>
/// <param name="Type">Plugin type string: provisioning | payment | registrar | identity | fulfilment | hosted | app.</param>
/// <param name="Category">Display category label, e.g. "Hosting / Provisioning".</param>
/// <param name="Color">Hex colour for the plugin logo block.</param>
/// <param name="IsLoaded">Whether the DLL was successfully loaded at startup; never true for a refused or removed row.</param>
/// <param name="IsBuiltIn">Whether the plugin ships with the build; a built-in plugin cannot be removed.</param>
/// <param name="IsEnabled">Whether the plugin's enabled switch is on, the same stored flag the integration settings screen reads; a loaded plugin that is switched off does nothing.</param>
/// <param name="Capabilities">
/// The capabilities the manifest declares (<c>storedCredentials</c>, <c>endpoints</c>), as the SDK spells them, in
/// manifest order; empty when it declares none and for a refused or removed row.
/// </param>
/// <param name="Status">Whether the row is a loaded, refused or removed plugin; serialised as the enum name.</param>
/// <param name="RefusedRule">The manifest rule a refused plugin breaks; <see langword="null"/> otherwise.</param>
/// <param name="RefusedDetail">Where and how the rule is broken; <see langword="null"/> unless refused.</param>
/// <param name="IconUrl">
/// Path of the admin icon endpoint (<c>/api/admin/plugins/{id}/icon?v={version}</c>, the plugin version in the address so an update is never shown the cached old icon) for a loaded plugin that ships an icon;
/// <see langword="null"/> when it has none, so the panel shows the colour block.
/// </param>
/// <param name="StagedVersion">
/// The version of a newer (or forced older) upload that waits to replace the running one at the next restart;
/// <see langword="null"/> when nothing is staged.
/// </param>
/// <param name="HostAccess">
/// The kinds of access to a client's hosting the manifest asks for (for example <c>ssh</c>), as the SDK spells them;
/// empty when it asks for none and for a refused or removed row.
/// </param>
/// <param name="Changelog">The release notes of the installed version; <see langword="null"/> when none.</param>
/// <param name="MinHostVersion">The lowest Hostpanel version the plugin runs on; <see langword="null"/> for no lower bound.</param>
/// <param name="MaxHostVersion">The highest Hostpanel version the plugin runs on; <see langword="null"/> for no upper bound.</param>
/// <param name="Source">
/// Where the package came from: built in for a plugin that ships with the build, otherwise an upload or the registry;
/// <see langword="null"/> for a plugin installed before installs were recorded and for a refused or removed row.
/// </param>
/// <param name="InstalledAt">When the plugin was first installed; <see langword="null"/> when no install was recorded.</param>
/// <param name="UpdatedAt">When a package last replaced the first one (an upgrade waits for the restart to run); <see langword="null"/> while none has.</param>
/// <param name="InstalledBy">The display name of the administrator who installed it, as it was then; <see langword="null"/> when unknown.</param>
/// <param name="Signer">Who vouched for the latest accepted package; <see langword="null"/> when no install was recorded.</param>
/// <param name="SignerKeyId">The id of the key that signed the latest accepted package; <see langword="null"/> when it is unsigned or not recorded.</param>
/// <param name="PackageSizeBytes">The size in bytes of the latest accepted archive; <see langword="null"/> when no install was recorded.</param>
/// <param name="PackageSha256">The lower-case hex SHA-256 of the latest accepted archive; <see langword="null"/> when no install was recorded.</param>
/// <param name="SdkPackageVersion">
/// The <c>Hostpanel.Sdk</c> package version the installed package was built on; <see langword="null"/> when its
/// manifest names none and for a refused or removed row.
/// </param>
/// <param name="LastChange">The last change made to the plugin and how it ended (applied, or failed with the reason and the restored version); <see langword="null"/> when none was recorded.</param>
public sealed record PluginListItemDto(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Type,
    string Category,
    string Color,
    bool IsLoaded,
    bool IsBuiltIn,
    bool IsEnabled,
    IReadOnlyList<string> Capabilities,
    PluginListStatus Status = PluginListStatus.Loaded,
    PluginManifestRule? RefusedRule = null,
    string? RefusedDetail = null,
    string? StagedVersion = null,
    string? IconUrl = null,
    IReadOnlyList<string>? HostAccess = null,
    string? Changelog = null,
    string? MinHostVersion = null,
    string? MaxHostVersion = null,
    PluginSource? Source = null,
    DateTimeOffset? InstalledAt = null,
    DateTimeOffset? UpdatedAt = null,
    string? InstalledBy = null,
    PluginSignerKind? Signer = null,
    string? SignerKeyId = null,
    long? PackageSizeBytes = null,
    string? PackageSha256 = null,
    string? SdkPackageVersion = null,
    PluginChangeDto? LastChange = null);
