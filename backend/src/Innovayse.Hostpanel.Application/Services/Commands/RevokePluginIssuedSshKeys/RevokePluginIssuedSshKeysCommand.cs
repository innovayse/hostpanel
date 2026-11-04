namespace Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;

/// <summary>
/// Revokes every live SSH key the host issued to a plugin, on whatever service and server it is, because the plugin was
/// removed: an access a removed plugin's operator can no longer manage must not outlive it. Dispatched when a plugin is
/// removed, never routed. Idempotent: only keys still live are touched.
/// </summary>
/// <param name="PluginId">The removed plugin.</param>
public sealed record RevokePluginIssuedSshKeysCommand(string PluginId);
