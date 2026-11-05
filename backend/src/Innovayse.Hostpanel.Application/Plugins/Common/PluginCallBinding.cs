using Innovayse.Hostpanel.Domain.Clients.Enums;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// What one plugin call is bound to: the plugin, the client the call acts for, the plugin's opaque reference of that
/// client and the permissions the caller holds on the account. It is built once per call by the area's handler and is the
/// only source of the client reference: the runner hands the same <see cref="ClientRef"/> to the plugin on
/// <c>PluginCaller.ClientRef</c> and to the store as its scope, and the host facts are filtered by the same
/// <see cref="Permissions"/>, so the caller, the facts and the store can never describe two different clients.
/// </summary>
/// <remarks>
/// Only two shapes exist, and only the two factories build them: <see cref="ForClient"/> (a client, its reference and the
/// caller's permissions) and <see cref="WithoutClient"/> (no client, no reference, no permissions). The constructor is
/// private and every property is get-only, so no caller can pair a client with a missing reference, a reference with no
/// client, or permissions with no account; a positional record would have allowed each through its public constructor
/// and <c>with</c>.
/// </remarks>
public sealed class PluginCallBinding
{
    /// <summary>The plugin being called; the store is scoped to it. Never blank.</summary>
    public string PluginId { get; }

    /// <summary>
    /// The host id of the verified client the call acts for, or <see langword="null"/> on an admin call that is not about a
    /// client (no host facts, and a plugin-wide store).
    /// </summary>
    public int? ClientId { get; }

    /// <summary>
    /// The plugin's opaque reference of <see cref="ClientId"/> (<c>IPluginReferences.ForClient</c>), never blank, or
    /// <see langword="null"/> exactly when <see cref="ClientId"/> is.
    /// </summary>
    public string? ClientRef { get; }

    /// <summary>
    /// The permissions the caller holds on the account; the host facts show only what they allow.
    /// <see cref="ClientPermission.None"/> on a call without a client.
    /// </summary>
    public ClientPermission Permissions { get; }

    /// <summary>
    /// The panel the call came through: <see cref="PluginArea.Client"/> for <see cref="ForClient"/>,
    /// <see cref="PluginArea.Admin"/> for <see cref="WithoutClient"/>. Access to a client's hosting is granted only on a
    /// client-area call, and this is where the host API reads the area from, never from the plugin.
    /// </summary>
    public PluginArea Area { get; }

    /// <summary>Builds a binding; only the factories call it, so only their two shapes exist.</summary>
    /// <param name="pluginId">The plugin being called, already checked not blank.</param>
    /// <param name="clientId">The client the call acts for, or <see langword="null"/> for none.</param>
    /// <param name="clientRef">The reference of <paramref name="clientId"/>, <see langword="null"/> exactly when it is.</param>
    /// <param name="permissions">The caller's permissions on the account, <see cref="ClientPermission.None"/> without one.</param>
    /// <param name="area">The panel the call came through.</param>
    private PluginCallBinding(string pluginId, int? clientId, string? clientRef, ClientPermission permissions, PluginArea area)
    {
        Area = area;
        PluginId = pluginId;
        ClientId = clientId;
        ClientRef = clientRef;
        Permissions = permissions;
    }

    /// <summary>The binding of a call that acts for one verified client.</summary>
    /// <param name="pluginId">The plugin being called.</param>
    /// <param name="clientId">The host id of the verified client the call acts for.</param>
    /// <param name="clientRef">The plugin's opaque reference of <paramref name="clientId"/>.</param>
    /// <param name="permissions">The permissions the caller holds on the account.</param>
    /// <returns>A binding carrying the client, its reference and the permissions.</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> or <paramref name="clientRef"/> is blank.</exception>
    public static PluginCallBinding ForClient(
        string pluginId, int clientId, string clientRef, ClientPermission permissions)
    {
        EnsurePluginId(pluginId);
        if (string.IsNullOrWhiteSpace(clientRef))
        {
            throw new ArgumentException("A client binding needs the client's reference.", nameof(clientRef));
        }

        return new PluginCallBinding(pluginId, clientId, clientRef, permissions, PluginArea.Client);
    }

    /// <summary>The binding of an admin call that is not about a client: no facts, a plugin-wide store.</summary>
    /// <param name="pluginId">The plugin being called.</param>
    /// <returns>A binding with no client, no reference and no permissions.</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> is blank.</exception>
    public static PluginCallBinding WithoutClient(string pluginId)
    {
        EnsurePluginId(pluginId);
        return new PluginCallBinding(pluginId, null, null, ClientPermission.None, PluginArea.Admin);
    }

    /// <summary>Refuses a blank plugin id, which would scope the store and the references to no plugin.</summary>
    /// <param name="pluginId">The plugin id to check.</param>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> is blank.</exception>
    private static void EnsurePluginId(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("A binding needs the plugin's id.", nameof(pluginId));
        }
    }
}
