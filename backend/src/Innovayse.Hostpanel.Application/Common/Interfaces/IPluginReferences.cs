namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The opaque references a plugin sees instead of host ids. A reference is stable for one plugin and one subject,
/// differs for every other plugin and cannot be turned back into the host id by the plugin.
/// </summary>
public interface IPluginReferences
{
    /// <summary>The reference of a client for one plugin.</summary>
    /// <param name="pluginId">The plugin the reference is for.</param>
    /// <param name="clientId">The host's client id.</param>
    /// <returns>The same text for the same plugin and client on every call, in every process of one deployment.</returns>
    string ForClient(string pluginId, int clientId);

    /// <summary>The reference of a signed-in user or member for one plugin.</summary>
    /// <param name="pluginId">The plugin the reference is for.</param>
    /// <param name="userId">The host's subject id of the user.</param>
    /// <returns>The same text for the same plugin and user on every call, in every process of one deployment.</returns>
    string ForUser(string pluginId, string userId);
}
