namespace Innovayse.Application.Reports.Commands.RevalidateSslMonitoring;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="RevalidateSslMonitoringCommand"/>.</summary>
/// <param name="sslService">Certificate check cache over the panel domains.</param>
public sealed class RevalidateSslMonitoringHandler(ISslMonitoringService sslService)
{
    /// <summary>
    /// Re-checks every domain certificate, replaces the cache, and answers with the refreshed
    /// report so the caller does not need a second round trip to read it.
    /// </summary>
    /// <param name="command">Whether inactive domains are checked as well.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed report, grouped by expiry bucket.</returns>
    public Task<SslMonitoringDto> HandleAsync(RevalidateSslMonitoringCommand command, CancellationToken ct)
        => sslService.RevalidateAsync(command.IncludeInactive, ct);
}
