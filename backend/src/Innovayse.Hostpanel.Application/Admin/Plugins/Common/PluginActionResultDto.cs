namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Result returned by install and remove plugin operations.
/// The client should display a "restart required" banner when <see cref="RequiresRestart"/> is true.
/// </summary>
/// <param name="RequiresRestart">
/// True when plugin files were installed or removed, which take effect only after a server restart; false when a removal
/// found the files already gone and only switched the plugin off and queued the purge of its data.
/// </param>
public sealed record PluginActionResultDto(bool RequiresRestart);
