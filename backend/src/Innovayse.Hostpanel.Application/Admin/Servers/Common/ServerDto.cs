using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// DTO for a provisioning server returned in list and detail views.
/// </summary>
/// <param name="Id">Unique server identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Endpoint">Where and how the server's API is reached.</param>
/// <param name="Module">Control panel module identifier string.</param>
/// <param name="Username">API username.</param>
/// <param name="Limits">Account capacity and whether the server takes new accounts.</param>
/// <param name="Hosting">Datacenter, status page and monthly cost.</param>
/// <param name="NameServers">Nameservers the server's accounts are delegated to.</param>
/// <param name="ServerGroupId">ID of the assigned group, or null.</param>
/// <param name="ServerGroupName">Name of the assigned group, or null.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="IsOnline">Whether the last connection test succeeded, or null if never tested.</param>
/// <param name="LastTestedAt">UTC timestamp of the last connection test, or null if never tested.</param>
/// <param name="AccountsCount">Account count reported by the server on last test, or null if unknown.</param>
/// <param name="Credentials">Which secrets are stored; the secrets themselves are never returned.</param>
/// <param name="Usage">What the active services on the server renew for; read by the list only, null where the server is shown inside its group.</param>
public sealed record ServerDto(
    int Id,
    string Name,
    ServerEndpointDto Endpoint,
    string Module,
    string Username,
    ServerLimitsDto Limits,
    ServerHostingDto Hosting,
    ServerNameServers NameServers,
    int? ServerGroupId,
    string? ServerGroupName,
    DateTimeOffset CreatedAt,
    bool? IsOnline,
    DateTimeOffset? LastTestedAt,
    int? AccountsCount,
    ServerCredentialStatusDto Credentials,
    ServerUsageDto? Usage = null);
