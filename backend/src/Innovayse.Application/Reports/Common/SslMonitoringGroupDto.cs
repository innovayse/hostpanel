namespace Innovayse.Application.Reports.Common;

/// <summary>One group in the SSL Monitoring report.</summary>
/// <param name="GroupName">Expiry bucket the rows fall into, such as Expired or Expiring soon.</param>
/// <param name="Rows">Domains in this bucket.</param>
public record SslMonitoringGroupDto(
    string GroupName,
    IReadOnlyList<SslDomainRowDto> Rows);
