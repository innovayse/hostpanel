namespace Innovayse.Application.Reports.Queries.GetSslMonitoring;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetSslMonitoringQuery"/>.</summary>
/// <param name="sslService">Certificate check cache over the panel domains.</param>
public sealed class GetSslMonitoringHandler(ISslMonitoringService sslService)
{
    /// <summary>
    /// Returns the last certificate check for every domain. Reads the cache only, so opening the
    /// report never waits on a live TLS handshake per domain; re-checking is a separate command.
    /// </summary>
    /// <param name="query">Whether inactive domains are included.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Domains grouped by how soon their certificate expires.</returns>
    public Task<SslMonitoringDto> HandleAsync(GetSslMonitoringQuery query, CancellationToken ct)
        => sslService.GetReportAsync(query.IncludeInactive, ct);
}
