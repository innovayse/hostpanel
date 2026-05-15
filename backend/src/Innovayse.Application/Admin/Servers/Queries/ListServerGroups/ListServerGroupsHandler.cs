namespace Innovayse.Application.Admin.Servers.Queries.ListServerGroups;
using Innovayse.Application.Admin.Servers.DTOs;
using Innovayse.Application.Admin.Servers.Queries.ListServers;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="ListServerGroupsQuery"/> by loading all groups with their servers.
/// </summary>
public sealed class ListServerGroupsHandler(IServerGroupRepository repo)
{
    /// <summary>
    /// Loads all server groups and maps them to <see cref="ServerGroupDto"/>.
    /// </summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of group DTOs.</returns>
    public async Task<List<ServerGroupDto>> HandleAsync(ListServerGroupsQuery query, CancellationToken ct)
    {
        var groups = await repo.ListAsync(ct);
        return groups.Select(ToDto).ToList();
    }

    /// <summary>
    /// Maps a <see cref="ServerGroup"/> to a <see cref="ServerGroupDto"/>.
    /// </summary>
    /// <param name="g">The server group entity.</param>
    /// <returns>Mapped DTO.</returns>
    private static ServerGroupDto ToDto(ServerGroup g)
    {
        var groupMap = new Dictionary<int, string> { [g.Id] = g.Name };
        return new ServerGroupDto(
            g.Id, g.Name, g.FillType.ToString(),
            g.Servers.Select(s => ListServersHandler.ToDto(s, groupMap)).ToList(),
            g.CreatedAt);
    }
}
