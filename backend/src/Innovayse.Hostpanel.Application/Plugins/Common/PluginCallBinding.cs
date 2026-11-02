namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// What the services of one plugin call are bound to: the plugin, and the client the call acts for. The store's client
/// scope is derived from these two (the plugin's opaque reference of the client), so the host facts and the store can
/// never be bound to two different clients.
/// </summary>
/// <param name="PluginId">The plugin being called; the store is scoped to it.</param>
/// <param name="ClientId">
/// The host id of the verified client the call acts for, in either area, or <see langword="null"/> on an admin call
/// that is not about a client (no host facts, and a plugin-wide store).
/// </param>
public sealed record PluginCallBinding(string PluginId, int? ClientId);
