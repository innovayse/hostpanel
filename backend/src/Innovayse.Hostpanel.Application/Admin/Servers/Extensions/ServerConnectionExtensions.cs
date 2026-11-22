using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Extensions;

/// <summary>Maps the host's <see cref="ServerConnectionDto"/> to the connection a panel plugin is handed.</summary>
public static class ServerConnectionExtensions
{
    /// <summary>
    /// Maps a server's connection details to the <see cref="PanelServerConnection"/> a panel plugin receives. The host keeps no
    /// panel port, so <see cref="PanelServerConnection.Port"/> is <see langword="null"/> and the plugin uses its own default. The plugin is handed
    /// <see cref="ServerConnectionDto.ApiEndpointHost"/> as the host name, the address the panel's API is dialled at, which is the server's
    /// host name unless an API address is set.
    /// </summary>
    /// <param name="server">The server's connection details.</param>
    /// <param name="password">
    /// The server's stored API password, or <see langword="null"/> when none is stored. It is not part of
    /// <see cref="ServerConnectionDto"/>, which every built in provider receives, so it is read only for a plugin call.
    /// </param>
    /// <returns>The connection to put in a panel plugin context.</returns>
    public static PanelServerConnection ToPanelConnection(this ServerConnectionDto server, string? password)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new PanelServerConnection(
            server.Id,
            server.Name,
            server.ApiEndpointHost,
            server.IpAddress,
            Port: null,
            server.UseSsl,
            server.Username,
            server.AccessHash,
            server.ApiToken,
            AccountId: null,
            password);
    }
}
