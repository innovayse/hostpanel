namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the SSL Monitoring report.</summary>
public record SslDomainRowDto(
    string DomainName,
    bool HasSsl,
    string? Issuer,
    string? ExpiresAt,
    string LastUpdate,
    bool IsActive);

/// <summary>One group in the SSL Monitoring report.</summary>
public record SslMonitoringGroupDto(
    string GroupName,
    IReadOnlyList<SslDomainRowDto> Rows);

/// <summary>Full SSL Monitoring report result.</summary>
public record SslMonitoringDto(
    IReadOnlyList<SslMonitoringGroupDto> Groups);
