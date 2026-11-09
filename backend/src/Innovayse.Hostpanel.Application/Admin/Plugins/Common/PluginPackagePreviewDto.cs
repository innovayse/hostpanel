namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// What a plugin package says about itself before it is installed, returned by <c>POST /api/admin/plugins/inspect</c> so the
/// admin can read what it asks for and confirm the install. Nothing is installed or checked beyond reading the manifest.
/// </summary>
/// <param name="Id">The plugin id from <c>plugin.json</c>.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The plugin version.</param>
/// <param name="Author">The author name or organisation.</param>
/// <param name="Description">The short description.</param>
/// <param name="HostAccess">
/// The kinds of access to a client's hosting the plugin asks for (for example <c>ssh</c>), as the SDK spells them, in
/// manifest order; empty when it asks for none.
/// </param>
/// <param name="Capabilities">
/// The capabilities the plugin declares (for example <c>storedCredentials</c>, <c>endpoints</c>), as the SDK spells them,
/// in manifest order; empty when it declares none.
/// </param>
/// <param name="Changelog">The release notes of this version as plain text; <see langword="null"/> when the manifest has none.</param>
/// <param name="MinHostVersion">The lowest Hostpanel version the plugin runs on; <see langword="null"/> for no lower bound.</param>
/// <param name="MaxHostVersion">The highest Hostpanel version the plugin runs on; <see langword="null"/> for no upper bound.</param>
/// <param name="HostVersion">The version of the running Hostpanel build, the one the range is compared with.</param>
/// <param name="Compatible">Whether the running Hostpanel version is inside the range; an install outside it is refused.</param>
public sealed record PluginPackagePreviewDto(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    IReadOnlyList<string> HostAccess,
    IReadOnlyList<string> Capabilities,
    string? Changelog,
    string? MinHostVersion,
    string? MaxHostVersion,
    string HostVersion,
    bool Compatible);
