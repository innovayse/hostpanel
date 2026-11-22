using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.CreateServer;

/// <summary>
/// Creates a new provisioning server with full configuration.
/// </summary>
/// <remarks>
/// The configuration is grouped by meaning into nested records, so the request body is nested the
/// same way; the handler assembles <see cref="ServerDetails"/> before calling <see cref="Server.Create"/>.
/// The nested members are required: a body that omits one is refused at binding.
/// </remarks>
/// <param name="Name">Display name for the server.</param>
/// <param name="Endpoint">Where and how the server's API is reached.</param>
/// <param name="Module">Control panel module id: a built in one or one a panel plugin declares; stored in the spelling the module catalog knows.</param>
/// <param name="Username">API username.</param>
/// <param name="Credentials">API secrets; members are null when the module does not use them.</param>
/// <param name="Limits">Account capacity and whether the server takes new accounts.</param>
/// <param name="Hosting">Datacenter, status page and monthly cost.</param>
/// <param name="NameServers">Nameservers the server's accounts are delegated to.</param>
public sealed record CreateServerCommand(
    string Name,
    [property: JsonRequired] ServerEndpointDto Endpoint,
    string Module,
    string Username,
    [property: JsonRequired] ServerCredentials Credentials,
    [property: JsonRequired] ServerLimitsDto Limits,
    [property: JsonRequired] ServerHostingDto Hosting,
    [property: JsonRequired] ServerNameServers NameServers);
