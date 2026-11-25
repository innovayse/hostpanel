namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>The panel plugin that serves a service's server, so a screen can name it and show its icon on the Open panel button.</summary>
/// <param name="Id">The plugin id as its manifest declares it; the icon routes take it.</param>
/// <param name="Name">The plugin's display name, the alternative text of its icon.</param>
/// <param name="HasIcon"><see langword="true"/> when the plugin ships an icon the icon routes serve.</param>
public sealed record ServicePanelPluginDto(string Id, string Name, bool HasIcon);
