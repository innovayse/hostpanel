using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>
/// Disk usage service. Returns cached stats from the database.
/// UpdateNowAsync polls all cPanel servers and upserts the cache.
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

    /// <summary>Initializes a new instance of the <see cref="DiskUsageService"/> class.</summary>
    /// <param name="db">The database the servers are read from and the cached figures are written to.</param>
    /// <param name="httpFactory">Creates the clients that query each hosting server.</param>
    /// <param name="logger">Records a server that could not be polled.</param>
    /// <param name="clock">The source of the current moment.</param>
    public DiskUsageService(AppDbContext db, IHttpClientFactory httpFactory, ILogger<DiskUsageService> logger, IClock clock)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DiskUsageDto> GetReportAsync(CancellationToken ct)
    {
        var stats = await _db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(stats);
    }

    /// <inheritdoc/>
    public async Task<DiskUsageDto> UpdateNowAsync(CancellationToken ct)
    {
        // Load all cPanel servers
        var servers = await _db.Servers
            .Where(s => s.Module == ServerModule.CPanel && !s.IsDisabled)
            .ToListAsync(ct);

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
            try
            {
                CpanelWhmApi api;
                if (!string.IsNullOrEmpty(server.ApiToken))
                {
                    api = CpanelWhmApi.Create(server.Hostname, server.Username, server.ApiToken, _httpFactory);
                }
                else if (!string.IsNullOrEmpty(server.AccessHash))
                {
                    api = CpanelWhmApi.CreateWithHash(server.Hostname, server.Username, server.AccessHash, _httpFactory);
                }
                else
                {
                    _logger.LogWarning("Server {Name} has no ApiToken or AccessHash, so the disk usage poll is skipped.", server.Name);
                    continue;
                }

                var accounts = await api.ListAccountsAsync(ct);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch disk usage from server {Name} ({Hostname}).", server.Name, server.Hostname);
            }
        }

        await _db.SaveChangesAsync(ct);

        var allStats = await _db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(allStats);
    }

    /// <summary>Groups the stored stats per server and formats the figures for display.</summary>
    /// <param name="stats">The latest stat row for each hosted domain.</param>
    /// <returns>The report with per-server totals.</returns>
    private static DiskUsageDto BuildReport(IReadOnlyList<Domain.Hosting.DiskUsageStat> stats)
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
        return new DiskUsageDto(servers, lastUpdated);
    }
}
