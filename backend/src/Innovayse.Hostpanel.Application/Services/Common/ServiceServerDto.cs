namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>The provisioning server a service is hosted on, as far as it could be resolved.</summary>
/// <param name="Name">Display name of the server.</param>
/// <param name="Hostname">Hostname of the server.</param>
/// <param name="IpAddress">IP address of the server, or <see langword="null"/> if it has none.</param>
/// <param name="PanelPlugin">The panel plugin that serves the server's module, or <see langword="null"/> when none does (a module the host provisions itself).</param>
public sealed record ServiceServerDto(string Name, string Hostname, string? IpAddress, ServicePanelPluginDto? PanelPlugin);
