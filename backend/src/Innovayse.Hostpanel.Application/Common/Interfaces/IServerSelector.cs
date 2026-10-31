using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Selects the optimal server for provisioning a new account.
/// Uses proportional fill strategy: picks the server with the lowest fill percentage,
/// breaking ties by choosing the server with the higher capacity.
/// </summary>
public interface IServerSelector
{
    /// <summary>
    /// Selects the best available server among those on any of the given modules.
    /// </summary>
    /// <param name="modules">The server modules a server may be on, e.g. every module a provider exists for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if no eligible server is available.</returns>
    Task<ServerConnectionDto?> SelectAsync(IReadOnlyCollection<ServerModule> modules, CancellationToken ct);

    /// <summary>
    /// Selects the best available server from a specific server group.
    /// </summary>
    /// <param name="groupId">The server group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if no eligible server is available.</returns>
    Task<ServerConnectionDto?> SelectFromGroupAsync(int groupId, CancellationToken ct);
}
