namespace Innovayse.Infrastructure.Reports;

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Services;
using Innovayse.Domain.Ssl;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>Checks SSL certificates and caches results in the database.</summary>
public sealed class SslMonitoringService(AppDbContext db) : ISslMonitoringService
{
    private static readonly string[] GroupOrder =
        ["Expires within 30 Days", "Expires within 90 Days", "Expires within 180 Days", "Expires in over 180 Days", "No SSL Detected"];

    /// <inheritdoc/>
    public async Task<SslMonitoringDto> GetReportAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.SslChecks.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var checks = await query.OrderBy(x => x.DomainName).ToListAsync(ct);
        return BuildReport(checks);
    }

    /// <inheritdoc/>
    public async Task<SslMonitoringDto> RevalidateAsync(bool includeInactive, CancellationToken ct)
    {
        // Get all domain names from the Domains table
        var domains = await db.Domains
            .Select(d => new { DomName = d.Name, IsActive = d.Status == DomainStatus.Active })
            .ToListAsync(ct);

        // Also get domains from client services
        var serviceDomains = await db.ClientServices
            .Where(s => s.Domain != null && s.Domain != "")
            .Select(s => new { Name = s.Domain!, IsActive = s.Status == ServiceStatus.Active })
            .ToListAsync(ct);

        // Load existing cache
        var existing = await db.SslChecks.ToDictionaryAsync(x => x.DomainName, ct);

        // Merge: DB domains + service domains + already-cached entries
        var allDomains = domains
            .Select(d => (Name: d.DomName, d.IsActive))
            .Concat(serviceDomains.Select(d => (d.Name, d.IsActive)))
            .Concat(existing.Values.Select(e => (Name: e.DomainName, e.IsActive)))
            .DistinctBy(d => d.Name)
            .ToList();

        // Check each domain in parallel (max 10 concurrent)
        var semaphore = new SemaphoreSlim(10, 10);
        var tasks = allDomains.Select(async domain =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sslResult = await CheckSslAsync(domain.Name);
                var hasSsl = sslResult.hasSsl;
                var issuer = sslResult.issuer;
                var expiresAt = sslResult.expiresAt;
                if (existing.TryGetValue(domain.Name, out var record))
                {
                    record.Update(hasSsl, issuer, expiresAt, domain.IsActive);
                }
                else
                {
                    db.SslChecks.Add(SslCheck.Create(domain.Name, hasSsl, issuer, expiresAt, domain.IsActive));
                }
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        await db.SaveChangesAsync(ct);

        return await GetReportAsync(includeInactive, ct);
    }

    private static async Task<(bool hasSsl, string? issuer, DateTimeOffset? expiresAt)> CheckSslAsync(string hostname)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(hostname, 443, cts.Token);

            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(hostname);

            if (ssl.RemoteCertificate is not X509Certificate2 cert)
            {
                return (false, null, null);
            }

            var expiry = new DateTimeOffset(cert.NotAfter, TimeSpan.Zero);
            var issuerCn = cert.Issuer
                .Split(',')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
                ?.Substring(2)
                ?? cert.Issuer
                    .Split(',')
                    .Select(p => p.Trim())
                    .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring(3)
                ?? cert.Issuer;

            return (true, issuerCn, expiry);
        }
        catch
        {
            return (false, null, null);
        }
    }

    private static SslMonitoringDto BuildReport(IReadOnlyList<SslCheck> checks)
    {
        var now = DateTimeOffset.UtcNow;

        string GetGroup(SslCheck c)
        {
            if (!c.HasSsl || c.ExpiresAt is null)
            {
                return "No SSL Detected";
            }

            var days = (c.ExpiresAt.Value - now).TotalDays;
            if (days <= 30)
            {
                return "Expires within 30 Days";
            }

            if (days <= 90)
            {
                return "Expires within 90 Days";
            }

            if (days <= 180)
            {
                return "Expires within 180 Days";
            }

            return "Expires in over 180 Days";
        }

        static string FormatLastUpdate(DateTimeOffset checkedAt)
        {
            var diff = DateTimeOffset.UtcNow - checkedAt;
            if (diff.TotalMinutes < 1)
            {
                return "just now";
            }

            if (diff.TotalMinutes < 60)
            {
                return $"{(int)diff.TotalMinutes} minutes ago";
            }

            if (diff.TotalHours < 24)
            {
                return $"{(int)diff.TotalHours} hours ago";
            }

            return $"{(int)diff.TotalDays} days ago";
        }

        var grouped = checks
            .GroupBy(GetGroup)
            .ToDictionary(g => g.Key, g => g.ToList());

        var groups = GroupOrder
            .Select(name => new SslMonitoringGroupDto(
                name,
                grouped.TryGetValue(name, out var rows)
                    ? rows.Select(r => new SslDomainRowDto(
                        r.DomainName,
                        r.HasSsl,
                        r.Issuer,
                        r.ExpiresAt?.ToString("dd/MM/yyyy HH:mm"),
                        FormatLastUpdate(r.CheckedAt),
                        r.IsActive)).ToList()
                    : []))
            .ToList();

        return new SslMonitoringDto(groups);
    }
}
