using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// The one place a host server connection becomes the SDK's <see cref="PanelServerConnection"/> and a host service
/// connection becomes the <see cref="PanelAccountContext"/> a provisioning operation receives.
/// </summary>
public static class PanelServerConnectionExtensions
{
    /// <summary>Maps a server's access to the SDK's server connection.</summary>
    /// <param name="server">The server and its stored credentials.</param>
    /// <returns>
    /// The connection. <c>Port</c> is <see langword="null"/> because the host keeps no panel port, and
    /// <c>AccountId</c> is <see langword="null"/> because the server form stores none yet.
    /// </returns>
    public static PanelServerConnection ToPanel(this PluginServerAccess server)
    {
        var connection = server.Connection;
        return new PanelServerConnection(
            connection.Id,
            connection.Name,
            connection.Hostname,
            connection.IpAddress,
            null,
            server.UseSsl,
            connection.Username,
            connection.AccessHash,
            connection.ApiToken,
            null,
            server.Password);
    }

    /// <summary>Maps a granted service connection to the SDK's account context.</summary>
    /// <param name="connection">A connection the service granted, so it has a server and a provisioning reference.</param>
    /// <returns>The server and the service with its provisioning reference.</returns>
    public static PanelAccountContext ToPanel(this GrantedPluginServiceConnection connection)
    {
        return new PanelAccountContext(
            connection.Server.ToPanel(),
            new PanelService(connection.ServiceId, connection.ProvisioningRef));
    }
}
