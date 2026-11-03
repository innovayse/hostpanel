using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

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
/// <param name="Type">Plugin type string: provisioning | payment | registrar | identity | fulfilment | hosted.</param>
/// <param name="Category">Display category label, e.g. "Hosting / Provisioning".</param>
/// <param name="Color">Hex colour for the plugin logo block.</param>
/// <param name="IsLoaded">Whether the DLL was successfully loaded at startup; never true for a refused or removed row.</param>
/// <param name="IsBuiltIn">Whether the plugin ships with the build; a built-in plugin cannot be removed.</param>
/// <param name="Status">Whether the row is a loaded, refused or removed plugin; serialised as the enum name.</param>
/// <param name="RefusedRule">The manifest rule a refused plugin breaks; <see langword="null"/> otherwise.</param>
/// <param name="RefusedDetail">Where and how the rule is broken; <see langword="null"/> unless refused.</param>
/// <param name="StagedVersion">
/// The version of a newer (or forced older) upload that waits to replace the running one at the next restart;
/// <see langword="null"/> when nothing is staged.
/// </param>
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
    PluginListStatus Status = PluginListStatus.Loaded,
    PluginManifestRule? RefusedRule = null,
    string? RefusedDetail = null,
    string? StagedVersion = null);
