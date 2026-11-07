using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Extensions;

/// <summary>
/// Maps a <see cref="Server"/> aggregate to <see cref="ServerDto"/> and to the
/// <see cref="ServerConnectionDto"/> provisioning builds its provider from. <see cref="ToDto"/> is shared by
/// <c>ListServersHandler</c> and <c>ListServerGroupsHandler</c>, which both need to render a
/// server row: one flat, one nested inside its group.
/// </summary>
public static class ServerMapper
{
    /// <summary>Maps a <see cref="Server"/> to the <see cref="ServerConnectionDto"/> a provisioning provider is built from.</summary>
    /// <param name="s">The server entity.</param>
    /// <returns>The connection details.</returns>
    public static ServerConnectionDto ToConnection(this Server s) =>
        new(s.Id, s.Name, s.Module, s.Hostname, s.IpAddress, s.AccessHash, s.Username, s.ApiToken);

    /// <summary>
    /// Maps a <see cref="Server"/> to a <see cref="ServerDto"/>.
    /// </summary>
    /// <param name="s">The server entity.</param>
    /// <param name="groupMap">Lookup of group ids to names.</param>
    /// <returns>The mapped DTO.</returns>
    public static ServerDto ToDto(Server s, IReadOnlyDictionary<int, string> groupMap) =>
        new(
            s.Id, s.Name,
            new ServerEndpointDto(s.Hostname, s.IpAddress, s.AssignedIpAddresses, s.UseSSL),
            s.Module.ToString(), s.Username,
            new ServerLimitsDto(s.MaxAccounts, s.IsDefault, s.IsDisabled),
            new ServerHostingDto(s.Datacenter, s.ServerStatusAddress, s.MonthlyCost),
            new ServerNameServers(
                s.Ns1Hostname, s.Ns1Ip, s.Ns2Hostname, s.Ns2Ip, s.Ns3Hostname, s.Ns3Ip,
                s.Ns4Hostname, s.Ns4Ip, s.Ns5Hostname, s.Ns5Ip),
            s.ServerGroupId,
            s.ServerGroupId.HasValue && groupMap.TryGetValue(s.ServerGroupId.Value, out var gName) ? gName : null,
            s.CreatedAt,
            s.IsOnline,
            s.LastTestedAt,
            s.AccountsCount,
            new ServerCredentialStatusDto(s.HasPassword, s.HasApiToken, s.HasAccessHash));
}
