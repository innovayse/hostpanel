namespace Innovayse.Application.Servers;

using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Selects the optimal server for provisioning using proportional fill strategy.
/// <para>
/// Logic: pick the server with the lowest fill percentage (accounts / maxAccounts).
/// If percentages are equal, pick the server with the higher capacity.
/// Servers that are disabled, offline, or full are excluded.
/// </para>
/// </summary>
/// <param name="serverRepo">Server repository for loading available servers.</param>
/// <param name="groupRepo">Server group repository for loading grouped servers.</param>
/// <param name="logger">Logger for selection diagnostics.</param>
public sealed class ServerSelector(
    IServerRepository serverRepo,
    IServerGroupRepository groupRepo,
    ILogger<ServerSelector> logger) : IServerSelector
{
    /// <summary>
    /// Selects the best available server from all servers of the given module type.
    /// </summary>
    /// <param name="module">The server module to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected server, or null if none are eligible.</returns>
    public async Task<Server?> SelectAsync(ServerModule module, CancellationToken ct)
    {
        var servers = await serverRepo.ListAsync((ServerModule?)module, ct);
        return SelectBest(servers);
    }

    /// <summary>
    /// Selects the best available server from a specific server group.
    /// </summary>
    /// <param name="groupId">The server group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected server, or null if none are eligible.</returns>
    public async Task<Server?> SelectFromGroupAsync(int groupId, CancellationToken ct)
    {
        var group = await groupRepo.FindByIdAsync(groupId, ct);

        if (group is null)
        {
            logger.LogWarning("Server group {GroupId} not found", groupId);
            return null;
        }

        return SelectBest(group.Servers);
    }

    /// <summary>
    /// Applies the proportional fill algorithm to a collection of servers.
    /// <para>
    /// 1. Excludes disabled servers and servers at full capacity.
    /// 2. Picks the server with the lowest fill percentage (accounts / maxAccounts).
    /// 3. Ties are broken by picking the server with the higher max capacity.
    /// 4. Servers with no capacity limit (MaxAccounts = null) are treated as having infinite capacity (0% fill).
    /// 5. Servers with unknown account count (AccountsCount = null) are treated as empty (0 accounts).
    /// </para>
    /// </summary>
    /// <param name="servers">Candidate servers to choose from.</param>
    /// <returns>The best server, or null if none qualify.</returns>
    private Server? SelectBest(IEnumerable<Server> servers)
    {
        var candidates = servers
            .Where(s => !s.IsDisabled)
            .Where(s => !IsFull(s))
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogWarning("No eligible servers available for provisioning");
            return null;
        }

        var selected = candidates
            .OrderBy(FillPercentage)
            .ThenByDescending(s => s.MaxAccounts ?? int.MaxValue)
            .First();

        logger.LogInformation(
            "Selected server {ServerName} (id={ServerId}) — {Accounts}/{Max} accounts ({Fill:P1} full)",
            selected.Name,
            selected.Id,
            selected.AccountsCount ?? 0,
            selected.MaxAccounts?.ToString() ?? "unlimited",
            FillPercentage(selected));

        return selected;
    }

    /// <summary>
    /// Calculates the fill percentage of a server.
    /// Returns 0.0 for servers with no capacity limit.
    /// </summary>
    /// <param name="server">The server to calculate for.</param>
    /// <returns>Fill percentage between 0.0 and 1.0.</returns>
    private static double FillPercentage(Server server)
    {
        if (server.MaxAccounts is null or 0)
        {
            return 0.0;
        }

        var accounts = server.AccountsCount ?? 0;
        return (double)accounts / server.MaxAccounts.Value;
    }

    /// <summary>
    /// Checks whether a server is at full capacity.
    /// </summary>
    /// <param name="server">The server to check.</param>
    /// <returns>True if the server cannot accept new accounts.</returns>
    private static bool IsFull(Server server)
    {
        if (server.MaxAccounts is null)
        {
            return false;
        }

        return (server.AccountsCount ?? 0) >= server.MaxAccounts.Value;
    }
}
