namespace Innovayse.Application.Reports.DTOs;

/// <summary>Full SSL Monitoring report result.</summary>
/// <param name="Groups">Domains grouped by expiry bucket.</param>
public record SslMonitoringDto(
    IReadOnlyList<SslMonitoringGroupDto> Groups);
