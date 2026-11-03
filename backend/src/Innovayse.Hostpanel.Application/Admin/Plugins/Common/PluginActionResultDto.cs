namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Result returned by install and remove plugin operations.
/// The client should display a "restart required" banner when <see cref="RequiresRestart"/> is true.
/// </summary>
/// <param name="RequiresRestart">
/// True when plugin files were installed or removed, which take effect only after a server restart; false when a removal
/// found the files already gone and only switched the plugin off and queued the purge of its data.
/// </param>
/// <param name="StagedVersion">
/// The version of an upgrade (or forced downgrade) that was staged and replaces the installed plugin at the next
/// restart; <see langword="null"/> for every other result.
/// </param>
public sealed record PluginActionResultDto(bool RequiresRestart, string? StagedVersion = null);
