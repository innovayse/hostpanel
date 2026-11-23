using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>
/// Disk usage service. Returns cached stats from the database.
/// UpdateNowAsync polls every cPanel server and every server a panel plugin serves, and upserts the cache; a server that
/// cannot be polled is answered as a failure instead of being left out silently. A server of any other module is no part of the report.
/// </summary>
public sealed class DiskUsageService : IDiskUsageService
{
    /// <summary>The database the servers are read from and the cached figures are written to.</summary>
    private readonly AppDbContext _db;

    /// <summary>Creates the clients that query each hosting server.</summary>
    private readonly IHttpClientFactory _httpFactory;

    /// <summary>Records a server that could not be polled.</summary>
    private readonly ILogger<DiskUsageService> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Finds the installed plugin that serves a server module.</summary>
    private readonly IServerModulePluginResolver _resolver;

    /// <summary>Reads the accounts of a server a panel plugin serves.</summary>
    private readonly PanelPluginAccountReader _pluginReader;

    /// <summary>Initializes a new instance of the <see cref="DiskUsageService"/> class.</summary>
    /// <param name="db">The database the servers are read from and the cached figures are written to.</param>
    /// <param name="httpFactory">Creates the clients that query each hosting server.</param>
    /// <param name="logger">Records a server that could not be polled.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="resolver">Finds the installed plugin that serves a server module.</param>
    /// <param name="pluginReader">Reads the accounts of a server a panel plugin serves.</param>
    public DiskUsageService(
        AppDbContext db,
        IHttpClientFactory httpFactory,
        ILogger<DiskUsageService> logger,
        IClock clock,
        IServerModulePluginResolver resolver,
        PanelPluginAccountReader pluginReader)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
        _clock = clock;
        _resolver = resolver;
        _pluginReader = pluginReader;
    }

    /// <inheritdoc/>
    public async Task<DiskUsageDto> GetReportAsync(CancellationToken ct)
    {
        var stats = await _db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(stats, []);
    }

    /// <inheritdoc/>
    public async Task<DiskUsageDto> UpdateNowAsync(CancellationToken ct)
    {
        // Load every enabled hosting server, the host's own panels and the ones a plugin serves; one whose panel cannot
        // be polled is answered as a failure below. Filtered after loading: which plugin serves a module is not in SQL.
        var servers = (await _db.Servers.Where(s => !s.IsDisabled).ToListAsync(ct))
            .Where(IsHostingServer)
            .ToList();
        var failures = new List<DiskUsageFailureDto>();

        // Load all client services with client info (to map cPanel domain → client name)
        var serviceRows = await (
            from svc in _db.ClientServices
            join client in _db.Clients on svc.ClientId equals client.Id
            where svc.Domain != null
            select new { svc.Domain, ClientName = client.FirstName + " " + client.LastName }
        ).ToListAsync(ct);

        // Build domain→clientName lookup (lowercase for case-insensitive match)
        var domainToClient = serviceRows
            .GroupBy(s => s.Domain!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().ClientName.Trim());

        // Load existing cache keyed by (serverName, domain)
        var existing = await _db.DiskUsageStats.ToListAsync(ct);
        var cache = existing.ToDictionary(
            s => (s.ServerName, s.Domain.ToLowerInvariant()),
            s => s);

        foreach (var server in servers)
        {
            var match = _resolver.Find(server.Module, PluginCapabilities.ProvisioningPanel);
            try
            {
                var accounts = await ReadAccountsAsync(server, match, ct);
                if (accounts is null)
                {
                    _logger.LogWarning("Server {Name} has no ApiToken or AccessHash, so the disk usage poll is skipped.", server.Name);
                    failures.Add(new DiskUsageFailureDto(server.Name, DiskUsageFailureReason.MissingCredentials));
                    continue;
                }

                Upsert(server, accounts, domainToClient, cache);
            }
            catch (DomainRuleException ex) when (ex.Code is HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED)
            {
                failures.Add(new DiskUsageFailureDto(server.Name, DiskUsageFailureReason.UnsupportedModule));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch disk usage from server {Name} ({Hostname}).", server.Name, server.Hostname);
                failures.Add(new DiskUsageFailureDto(server.Name, DiskUsageFailureReason.Unreachable));
            }
        }

        await _db.SaveChangesAsync(ct);

        var allStats = await _db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(allStats, failures);
    }

    /// <summary>Tells whether a server hosts client accounts: one of the host's panel modules, or one an installed panel plugin serves.</summary>
    /// <param name="server">The server.</param>
    /// <returns><see langword="true"/> when the server is part of the disk usage report.</returns>
    private bool IsHostingServer(Server server)
    {
        return ServerModules.Known.Any(module => ServerModules.Same(module, server.Module))
            || _resolver.Find(server.Module, PluginCapabilities.ProvisioningPanel).Outcome is not ServerModulePluginOutcome.NotServed;
    }

    /// <summary>Reads the accounts of one server from the plugin that serves its module, or from WHM when no plugin does.</summary>
    /// <param name="server">The server.</param>
    /// <param name="match">The plugin that serves the server's module, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The accounts, or <see langword="null"/> when a cPanel server stores neither an API token nor an access hash.</returns>
    /// <exception cref="DomainRuleException">Thrown with <c>PANEL_PLUGIN_CONFLICT</c> when two plugins serve the module, and with a panel code when the plugin refuses.</exception>
    private async Task<IReadOnlyList<HostedAccountUsage>?> ReadAccountsAsync(Server server, ServerModulePluginMatch match, CancellationToken ct)
    {
        switch (match.Outcome)
        {
            case ServerModulePluginOutcome.Served:
                return await _pluginReader.ReadAsync(match.PluginId!, server.ToConnection().ToPanelConnection(server.Password), ct);
            case ServerModulePluginOutcome.Conflict:
                throw new DomainRuleException(HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT, "More than one plugin serves the server's module.");
        }

        CpanelWhmApi api;
        if (!string.IsNullOrEmpty(server.ApiToken))
        {
            api = CpanelWhmApi.Create(server.ApiEndpointHost, server.Username, server.ApiToken, _httpFactory);
        }
        else if (!string.IsNullOrEmpty(server.AccessHash))
        {
            api = CpanelWhmApi.CreateWithHash(server.ApiEndpointHost, server.Username, server.AccessHash, _httpFactory);
        }
        else
        {
            return null;
        }

        var accounts = await api.ListAccountsAsync(ct);
        return accounts
            .Select(acct => new HostedAccountUsage(acct.Username, acct.Domain, acct.DiskUsedMb, acct.DiskLimitMb, acct.BwUsedMb, acct.BwLimitMb))
            .ToList();
    }

    /// <summary>Writes one server's account usage into the cache, the one place both the cPanel and the plugin path store it.</summary>
    /// <param name="server">The server the accounts live on.</param>
    /// <param name="accounts">The usage the panel reported.</param>
    /// <param name="domainToClient">Client name per lower cased domain, to label each row.</param>
    /// <param name="cache">The cached rows keyed by server name and lower cased domain; new rows are added to it.</param>
    private void Upsert(
        Server server,
        IReadOnlyList<HostedAccountUsage> accounts,
        IReadOnlyDictionary<string, string> domainToClient,
        Dictionary<(string ServerName, string Domain), Domain.Hosting.DiskUsageStat> cache)
    {
        var fetchedAt = _clock.UtcNow;

        foreach (var acct in accounts)
        {
            var clientName = domainToClient.TryGetValue(acct.Domain.ToLowerInvariant(), out var cn) ? cn
                : domainToClient.TryGetValue(acct.Username.ToLowerInvariant(), out var cu) ? cu
                : acct.Username;

            var key = (server.Name, acct.Domain.ToLowerInvariant());
            if (cache.TryGetValue(key, out var stat))
            {
                stat.RecordUsage(acct.DiskUsedMb, acct.DiskLimitMb, acct.BwUsedMb, acct.BwLimitMb, fetchedAt);
            }
            else
            {
                var newStat = Domain.Hosting.DiskUsageStat.Create(
                    server.Name, acct.Domain, clientName,
                    acct.DiskUsedMb, acct.DiskLimitMb, acct.BwUsedMb, acct.BwLimitMb, fetchedAt);
                _db.DiskUsageStats.Add(newStat);
                cache[key] = newStat;
            }
        }
    }

    /// <summary>Groups the stored stats per server and formats the figures for display.</summary>
    /// <param name="stats">The latest stat row for each hosted domain.</param>
    /// <param name="failures">The servers the refresh could not read; empty for a plain read.</param>
    /// <returns>The report with per-server totals.</returns>
    private static DiskUsageDto BuildReport(IReadOnlyList<Domain.Hosting.DiskUsageStat> stats, IReadOnlyList<DiskUsageFailureDto> failures)
    {
        static string FormatMb(long mb)
        {
            if (mb == 0)
            {
                return "∞";
            }

            if (mb >= 1024)
            {
                return $"{mb / 1024.0:F2} GB";
            }

            return $"{mb} MB";
        }

        static int Pct(long used, long limit) =>
            limit == 0 ? 0 : (int)Math.Min(100, Math.Round(used * 100.0 / limit));

        var servers = stats
            .GroupBy(s => s.ServerName)
            .Select(grp => new DiskUsageServerDto(
                grp.Key,
                grp.Select(s => new DiskUsageRowDto(
                    s.ClientName,
                    s.Domain,
                    FormatMb(s.DiskUsageMb),
                    FormatMb(s.DiskLimitMb),
                    Pct(s.DiskUsageMb, s.DiskLimitMb),
                    FormatMb(s.BwUsageMb),
                    FormatMb(s.BwLimitMb),
                    Pct(s.BwUsageMb, s.BwLimitMb)))
                .ToList()))
            .ToList();

        var lastUpdated = stats.Count > 0 ? stats.Max(s => s.UpdatedAt) : (DateTimeOffset?)null;
        return new DiskUsageDto(servers, lastUpdated, failures);
    }
}
