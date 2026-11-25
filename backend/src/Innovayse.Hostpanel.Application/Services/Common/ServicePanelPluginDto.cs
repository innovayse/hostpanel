namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>The panel plugin that serves a service's server, so a screen can name it and show its icon on the Open panel button.</summary>
/// <param name="Id">The plugin id as its manifest declares it; the icon routes take it.</param>
/// <param name="Name">The plugin's display name, the alternative text of its icon.</param>
/// <param name="IconVersion">
/// The plugin's version when it ships an icon the icon routes serve, <see langword="null"/> when it ships none. The
/// icon routes are cached by address, so a screen puts the version in the address and an update shows the new icon at
/// once, the way the <c>iconUrl</c> of the plugin list does.
/// </param>
public sealed record ServicePanelPluginDto(string Id, string Name, string? IconVersion);
