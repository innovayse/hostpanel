namespace Innovayse.Application.Admin.Servers.Queries.ListServers;
using Innovayse.Application.Admin.Servers.DTOs;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="ListServersQuery"/> by loading all servers from the repository.
/// </summary>
public sealed class ListServersHandler(IServerRepository repo, IServerGroupRepository groupRepo)
{
    /// <summary>
    /// Loads servers and maps them to <see cref="ServerDto"/>.
    /// </summary>
    /// <param name="query">The list query with optional module filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of server DTOs.</returns>
    public async Task<List<ServerDto>> HandleAsync(ListServersQuery query, CancellationToken ct)
    {
        var servers = await repo.ListAsync(query.Module, ct);
        var groups = await groupRepo.ListAsync(ct);
        var groupMap = groups.ToDictionary(g => g.Id, g => g.Name);
        return servers.Select(s => ToDto(s, groupMap)).ToList();
    }

    /// <summary>
    /// Maps a <see cref="Server"/> to a <see cref="ServerDto"/>.
    /// </summary>
    /// <param name="s">The server entity.</param>
    /// <param name="groupMap">Lookup of group IDs to names.</param>
    /// <returns>Mapped DTO.</returns>
    internal static ServerDto ToDto(Server s, Dictionary<int, string> groupMap) =>
        new(
            s.Id, s.Name, s.Hostname, s.IpAddress, s.AssignedIpAddresses,
            s.Module.ToString(), s.Username, s.UseSSL,
            s.MaxAccounts, s.IsDefault, s.IsDisabled,
            s.MonthlyCost, s.Datacenter, s.ServerStatusAddress,
            s.Ns1Hostname, s.Ns1Ip,
            s.Ns2Hostname, s.Ns2Ip,
            s.Ns3Hostname, s.Ns3Ip,
            s.Ns4Hostname, s.Ns4Ip,
            s.Ns5Hostname, s.Ns5Ip,
            s.ServerGroupId,
            s.ServerGroupId.HasValue && groupMap.TryGetValue(s.ServerGroupId.Value, out var gName) ? gName : null,
            s.CreatedAt,
            s.IsOnline,
            s.LastTestedAt,
            s.AccountsCount);
}
