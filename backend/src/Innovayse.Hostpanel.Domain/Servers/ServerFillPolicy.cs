using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// The rule that decides which server receives the next hosting account, driven by the group's
/// <see cref="ServerFillType"/>.
/// <para>
/// Every fill type first drops disabled servers and servers at full capacity (a server whose limit is 0
/// is full from the start). A server whose account count is unknown counts as empty.
/// </para>
/// <para>
/// <see cref="ServerFillType.LeastFull"/>: the lowest fill ratio (accounts / maximum accounts) wins; a tie
/// goes to the default server, then to the higher maximum capacity. A server with no limit has a 0% fill
/// and unlimited capacity.
/// </para>
/// <para>
/// <see cref="ServerFillType.FillUntilFull"/>: the default server first, then servers with a limit before
/// unlimited ones, then by name and id, so one server is filled to capacity before the next is used.
/// </para>
/// A tie on every key keeps the order the candidates were given in.
/// </summary>
public static class ServerFillPolicy
{
    /// <summary>
    /// Picks the server that should receive the next account.
    /// </summary>
    /// <param name="servers">Candidate servers, in the order that settles a full tie.</param>
    /// <param name="fillType">How the group distributes accounts across its servers.</param>
    /// <returns>The chosen server, or <see langword="null"/> when no candidate is eligible.</returns>
    public static Server? SelectBest(IEnumerable<Server> servers, ServerFillType fillType)
    {
        // A server with MaxAccounts = 0 has IsFull true (0 accounts >= 0), so it is dropped with the full ones.
        var eligible = servers
            .Where(s => !s.IsDisabled)
            .Where(s => !IsFull(s));

        // OrderBy is a stable sort, so the caller's order decides between two servers that tie on every key.
        return fillType == ServerFillType.FillUntilFull
            ? eligible
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.MaxAccounts is null)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ThenBy(s => s.Id)
                .FirstOrDefault()
            : eligible
                .OrderBy(FillRatio)
                .ThenByDescending(s => s.IsDefault)
                .ThenByDescending(s => s.MaxAccounts ?? int.MaxValue)
                .FirstOrDefault();
    }

    /// <summary>
    /// Calculates how full a server is.
    /// </summary>
    /// <param name="server">The server to measure.</param>
    /// <returns>The fill ratio between 0.0 and 1.0; 0.0 for a server with no capacity limit.</returns>
    public static double FillRatio(Server server)
    {
        if (server.MaxAccounts is null or 0)
        {
            return 0.0;
        }

        var accounts = server.AccountsCount ?? 0;
        return (double)accounts / server.MaxAccounts.Value;
    }

    /// <summary>
    /// Checks whether a server can take no more accounts.
    /// </summary>
    /// <param name="server">The server to check.</param>
    /// <returns><see langword="true"/> when the server has a limit and has reached it.</returns>
    public static bool IsFull(Server server)
    {
        if (server.MaxAccounts is null)
        {
            return false;
        }

        return (server.AccountsCount ?? 0) >= server.MaxAccounts.Value;
    }
}
