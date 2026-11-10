using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssl;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>Checks SSL certificates and caches results in the database.</summary>
public sealed class SslMonitoringService : ISslMonitoringService
{
    /// <summary>The database the domains are read from and the certificate checks are written to.</summary>
    private readonly AppDbContext _db;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="SslMonitoringService"/> class.</summary>
    /// <param name="db">The database the domains are read from and the certificate checks are written to.</param>
    /// <param name="clock">The source of the current moment.</param>
    public SslMonitoringService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Display order of the expiry groups, soonest expiry first and "no SSL" last.</summary>
    private static readonly string[] GroupOrder =
        ["Expires within 30 Days", "Expires within 90 Days", "Expires within 180 Days", "Expires in over 180 Days", "No SSL Detected"];

    /// <inheritdoc/>
    public async Task<SslMonitoringDto> GetReportAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _db.SslChecks.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var checks = await query.OrderBy(x => x.DomainName).ToListAsync(ct);
        return BuildReport(checks, _clock.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<SslMonitoringDto> RevalidateAsync(bool includeInactive, CancellationToken ct)
    {
        // Get all domain names from the Domains table
        var domains = await _db.Domains
            .Select(d => new { DomName = d.Name, IsActive = d.Status == DomainStatus.Active })
            .ToListAsync(ct);

        // Also get domains from client services
        var serviceDomains = await _db.ClientServices
            .Where(s => s.Domain != null && s.Domain != "")
            .Select(s => new { Name = s.Domain!, IsActive = s.Status == ServiceStatus.Active })
            .ToListAsync(ct);

        // Load existing cache
        var existing = await _db.SslChecks.ToDictionaryAsync(x => x.DomainName, ct);

        // Merge: DB domains + service domains + already-cached entries
        var allDomains = domains
            .Select(d => (Name: d.DomName, d.IsActive))
            .Concat(serviceDomains.Select(d => (d.Name, d.IsActive)))
            .Concat(existing.Values.Select(e => (Name: e.DomainName, e.IsActive)))
            .DistinctBy(d => d.Name)
            .ToList();

        // Probe each domain in parallel (max 10 concurrent), then record the results
        // on one thread.
        //
        // The split is the point. Probing is network-bound and belongs in parallel, but
        // `db` is a single AppDbContext and EF Core's change tracker is not thread-safe:
        // ten tasks calling Add and mutating tracked entities on it at once can corrupt
        // its state or throw "A second operation was started on this context instance".
        // The semaphore bounded the outbound connections, never the writes: it let ten
        // of them run concurrently by design.
        var semaphore = new SemaphoreSlim(10, 10);
        var probes = allDomains.Select(async domain =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return new DomainSslProbe(domain.Name, domain.IsActive, await CheckSslAsync(domain.Name, ct));
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(probes);
        var checkedAt = _clock.UtcNow;

        foreach (var (name, isActive, (hasSsl, issuer, expiresAt)) in results)
        {
            if (existing.TryGetValue(name, out var record))
            {
                record.RecordCheck(hasSsl, issuer, expiresAt, isActive, checkedAt);
            }
            else
            {
                _db.SslChecks.Add(SslCheck.Create(name, hasSsl, issuer, expiresAt, isActive, checkedAt));
            }
        }

        await _db.SaveChangesAsync(ct);

        return await GetReportAsync(includeInactive, ct);
    }

    /// <summary>Reads the certificate a domain presents on port 443.</summary>
    /// <param name="hostname">The domain, which a client may have typed: only a public host is dialled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether a certificate was presented, its issuer and its expiry; empty values when the host is refused or does not answer.</returns>
    private static async Task<SslProbeResult> CheckSslAsync(string hostname, CancellationToken ct)
    {
        var resolution = await PublicHostGuard.ResolveAsync(hostname, ct);
        if (resolution.Verdict != PublicHostVerdict.Public)
        {
            return new SslProbeResult(false, null, null);
        }

        var certificate = await TlsCertificateReader.ReadAsync(hostname, resolution.Addresses, ct);
        return certificate is null
            ? new SslProbeResult(false, null, null)
            : new SslProbeResult(true, certificate.Issuer, certificate.NotAfter);
    }

    /// <summary>Buckets the stored checks into the expiry groups.</summary>
    /// <param name="checks">The stored check results to group.</param>
    /// <param name="now">The moment expiry and last-update ages are measured from.</param>
    /// <returns>The report, one group per expiry bucket in <c>GroupOrder</c>.</returns>
    private static SslMonitoringDto BuildReport(IReadOnlyList<SslCheck> checks, DateTimeOffset now)
    {
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

        string FormatLastUpdate(DateTimeOffset checkedAt)
        {
            var diff = now - checkedAt;
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
