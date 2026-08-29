namespace Innovayse.Application.Reports.Common;

/// <summary>Full SSL Monitoring report result.</summary>
/// <param name="Groups">Domains grouped by expiry bucket.</param>
public record SslMonitoringDto(
    IReadOnlyList<SslMonitoringGroupDto> Groups);
