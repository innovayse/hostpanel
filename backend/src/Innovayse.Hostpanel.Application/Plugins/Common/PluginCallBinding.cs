namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>What the services of one plugin call are bound to: the plugin, and the client the call acts for.</summary>
/// <param name="PluginId">The plugin being called; the store is scoped to it.</param>
/// <param name="ClientId">The host id of the client the call acts for, or <see langword="null"/> on an admin call outside any client.</param>
/// <param name="ClientRef">The plugin's opaque reference of that client, or <see langword="null"/> when there is no client.</param>
public sealed record PluginCallBinding(string PluginId, int? ClientId, string? ClientRef);
