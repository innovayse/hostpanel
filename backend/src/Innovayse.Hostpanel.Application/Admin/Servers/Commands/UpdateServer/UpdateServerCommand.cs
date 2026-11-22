using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.UpdateServer;

/// <summary>
/// Updates an existing provisioning server's full configuration.
/// </summary>
/// <remarks>
/// Grouped the same way as <see cref="Commands.CreateServer.CreateServerCommand"/>, plus the credentials
/// to remove. The nested members other than <c>Clears</c> are required: a body that omits one is
/// refused at binding.
/// </remarks>
/// <param name="Id">
/// Server primary key; wins over any id in the body. A value type, so it is left non-nullable: a
/// missing <c>int</c> in the body binds to <c>0</c> rather than being treated as a required
/// member, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">Display name for the server.</param>
/// <param name="Endpoint">Where and how the server's API is reached.</param>
/// <param name="Module">Control panel module id: a built in one or one a panel plugin declares; stored in the spelling the module catalog knows.</param>
/// <param name="Username">API username.</param>
/// <param name="Credentials">API secrets; a null, empty or whitespace member leaves the stored one unchanged.</param>
/// <param name="Limits">Account capacity and whether the server takes new accounts.</param>
/// <param name="Hosting">Datacenter, status page and monthly cost.</param>
/// <param name="NameServers">Nameservers the server's accounts are delegated to.</param>
/// <param name="Clears">Stored secrets to remove; a clear wins over a value sent alongside it. Null removes nothing.</param>
public sealed record UpdateServerCommand(
    int Id,
    string Name,
    [property: JsonRequired] ServerEndpointDto Endpoint,
    string Module,
    string Username,
    [property: JsonRequired] ServerCredentials Credentials,
    [property: JsonRequired] ServerLimitsDto Limits,
    [property: JsonRequired] ServerHostingDto Hosting,
    [property: JsonRequired] ServerNameServers NameServers,
    ServerCredentialClears? Clears = null);
