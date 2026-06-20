namespace Innovayse.Infrastructure.Reports;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Infrastructure.Provisioning.CPanel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Disk usage service. Returns cached stats from the database.
/// UpdateNowAsync polls all cPanel servers and upserts the cache.
/// </summary>
public sealed class DiskUsageService(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    ILogger<DiskUsageService> logger) : IDiskUsageService
{
    /// <inheritdoc/>
    public async Task<DiskUsageDto> GetReportAsync(CancellationToken ct)
    {
        var stats = await db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(stats);
    }

    /// <inheritdoc/>
    public async Task<DiskUsageDto> UpdateNowAsync(CancellationToken ct)
    {
        // Load all cPanel servers
        var servers = await db.Servers
            .Where(s => s.Module == ServerModule.CPanel && !s.IsDisabled)
            .ToListAsync(ct);

        // Load all client services with client info (to map cPanel domain → client name)
        var serviceRows = await (
            from svc in db.ClientServices
            join client in db.Clients on svc.ClientId equals client.Id
            where svc.Domain != null
            select new { svc.Domain, ClientName = client.FirstName + " " + client.LastName }
        ).ToListAsync(ct);

        // Build domain→clientName lookup (lowercase for case-insensitive match)
        var domainToClient = serviceRows
            .GroupBy(s => s.Domain!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().ClientName.Trim());

        // Load existing cache keyed by (serverName, domain)
        var existing = await db.DiskUsageStats.ToListAsync(ct);
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
                    api = CpanelWhmApi.Create(server.Hostname, server.Username, server.ApiToken, httpFactory);
                }
                else if (!string.IsNullOrEmpty(server.AccessHash))
                {
                    api = CpanelWhmApi.CreateWithHash(server.Hostname, server.Username, server.AccessHash, httpFactory);
                }
                else
                {
                    logger.LogWarning("Server {Name} has no ApiToken or AccessHash — skipping disk usage poll.", server.Name);
                    continue;
                }

                var accounts = await api.ListAccountsAsync(ct);

                foreach (var acct in accounts)
                {
                    var clientName = domainToClient.TryGetValue(acct.Domain.ToLowerInvariant(), out var cn) ? cn
                        : domainToClient.TryGetValue(acct.Username.ToLowerInvariant(), out var cu) ? cu
                        : acct.Username;

                    var key = (server.Name, acct.Domain.ToLowerInvariant());
                    if (cache.TryGetValue(key, out var stat))
                    {
                        stat.Update(acct.DiskUsedMb, acct.DiskLimitMb, acct.BwUsedMb, acct.BwLimitMb);
                    }
                    else
                    {
                        var newStat = Domain.Hosting.DiskUsageStat.Create(
                            server.Name, acct.Domain, clientName,
                            acct.DiskUsedMb, acct.DiskLimitMb, acct.BwUsedMb, acct.BwLimitMb);
                        db.DiskUsageStats.Add(newStat);
                        cache[key] = newStat;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch disk usage from server {Name} ({Hostname}).", server.Name, server.Hostname);
            }
        }

        await db.SaveChangesAsync(ct);

        var allStats = await db.DiskUsageStats.OrderBy(s => s.ServerName).ThenBy(s => s.Domain).ToListAsync(ct);
        return BuildReport(allStats);
    }

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
