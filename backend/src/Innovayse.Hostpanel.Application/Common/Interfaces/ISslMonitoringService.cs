using Innovayse.Hostpanel.Application.Reports.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Service for checking and caching SSL certificate status for domains.</summary>
public interface ISslMonitoringService
{
    /// <summary>Returns the current cached SSL results grouped by expiry bucket.</summary>
    /// <param name="includeInactive">Whether domains whose service or registration is no longer active are included; false limits the report to live domains.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached certificate results, grouped by how soon each expires.</returns>
    public Task<SslMonitoringDto> GetReportAsync(bool includeInactive, CancellationToken ct);

    /// <summary>Re-checks all domains and updates the cache, then returns grouped results.</summary>
    /// <param name="includeInactive">Whether domains whose service or registration is no longer active are included; false limits the report to live domains.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fresh certificate results, grouped by how soon each expires.</returns>
    public Task<SslMonitoringDto> RevalidateAsync(bool includeInactive, CancellationToken ct);
}
