namespace Innovayse.Application.Reports.Interfaces;

using Innovayse.Application.Reports.DTOs;

/// <summary>Service for checking and caching SSL certificate status for domains.</summary>
public interface ISslMonitoringService
{
    /// <summary>Returns the current cached SSL results grouped by expiry bucket.</summary>
    Task<SslMonitoringDto> GetReportAsync(bool includeInactive, CancellationToken ct);

    /// <summary>Re-checks all domains and updates the cache, then returns grouped results.</summary>
    Task<SslMonitoringDto> RevalidateAsync(bool includeInactive, CancellationToken ct);
}
