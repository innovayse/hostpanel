namespace Innovayse.Application.Admin.Plugins.DTOs;

/// <summary>
/// Result returned by install and remove plugin operations.
/// The client should display a "restart required" banner when <see cref="RequiresRestart"/> is true.
/// </summary>
/// <param name="RequiresRestart">Always true — plugin changes require a server restart to take effect.</param>
public record PluginActionResultDto(bool RequiresRestart);
