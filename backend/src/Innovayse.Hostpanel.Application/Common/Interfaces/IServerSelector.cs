using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Selects the optimal server for provisioning a new account.
/// A server group chooses by its own fill type: least full picks the lowest fill percentage, fill until full
/// takes servers in order until each is at capacity. The default server breaks ties and leads a fill until full.
/// With no group the least full server is chosen.
/// </summary>
public interface IServerSelector
{
    /// <summary>
    /// Selects the best available server among those on any of the given modules.
    /// </summary>
    /// <param name="modules">The server modules a server may be on, e.g. every module a provider exists for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if no eligible server is available.</returns>
    public Task<ServerConnectionDto?> SelectAsync(IReadOnlyCollection<ServerModule> modules, CancellationToken ct);

    /// <summary>
    /// Selects the best available server from a specific server group, among its servers on any of
    /// the given modules.
    /// </summary>
    /// <param name="groupId">The server group identifier.</param>
    /// <param name="modules">The server modules a server may be on; a group member on any other is skipped.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if no eligible server is available.</returns>
    public Task<ServerConnectionDto?> SelectFromGroupAsync(int groupId, IReadOnlyCollection<ServerModule> modules, CancellationToken ct);

    /// <summary>Lists which of the given modules have at least one enabled server.</summary>
    /// <param name="modules">The modules to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The modules, among <paramref name="modules"/>, that some enabled server is on.</returns>
    public Task<IReadOnlyCollection<ServerModule>> ModulesInUseAsync(IReadOnlyCollection<ServerModule> modules, CancellationToken ct);
}
