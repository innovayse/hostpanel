using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Loads the candidate servers for a provisioning run and hands them to <see cref="ServerFillPolicy"/>,
/// which decides which one receives the account.
/// </summary>
public sealed class ServerSelector : IServerSelector
{
    /// <summary>The module's data context, for loading candidate servers and groups.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Writes which server was chosen, or why none was.</summary>
    private readonly ILogger<ServerSelector> _logger;

    /// <summary>Initializes the selector.</summary>
    /// <param name="context">The module's data context, for loading candidate servers and groups.</param>
    /// <param name="logger">Writes which server was chosen, or why none was.</param>
    public ServerSelector(IHostpanelDbContext context, ILogger<ServerSelector> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Selects the best available server from all servers on any of the given modules.
    /// </summary>
    /// <param name="modules">The server modules a server may be on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if none are eligible.</returns>
    public async Task<ServerConnectionDto?> SelectAsync(IReadOnlyCollection<ServerModule> modules, CancellationToken ct)
    {
        // Ordered by name because the policy keeps the given order when two servers tie on fill and capacity.
        var servers = await _context.Servers
            .AsNoTracking()
            .Where(s => modules.Contains(s.Module))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        return Choose(servers);
    }

    /// <summary>
    /// Selects the best available server from a specific server group.
    /// </summary>
    /// <param name="groupId">The server group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How to reach the selected server, or null if none are eligible.</returns>
    public async Task<ServerConnectionDto?> SelectFromGroupAsync(int groupId, CancellationToken ct)
    {
        var group = await _context.ServerGroups
            .AsNoTracking()
            .Include(g => g.Servers)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
        {
            _logger.LogWarning("Server group {GroupId} not found", groupId);
            return null;
        }

        return Choose(group.Servers);
    }

    /// <summary>
    /// Applies the fill policy and logs the outcome.
    /// </summary>
    /// <param name="servers">Candidate servers to choose from.</param>
    /// <returns>How to reach the chosen server, or null if none qualify.</returns>
    private ServerConnectionDto? Choose(IEnumerable<Server> servers)
    {
        var selected = ServerFillPolicy.SelectBest(servers);
        if (selected is null)
        {
            _logger.LogWarning("No eligible servers available for provisioning");
            return null;
        }

        _logger.LogInformation(
            "Selected server {ServerName} (id={ServerId}), {Accounts}/{Max} accounts ({Fill:P1} full)",
            selected.Name,
            selected.Id,
            selected.AccountsCount ?? 0,
            selected.MaxAccounts?.ToString() ?? "unlimited",
            ServerFillPolicy.FillRatio(selected));

        return selected.ToConnection();
    }
}
