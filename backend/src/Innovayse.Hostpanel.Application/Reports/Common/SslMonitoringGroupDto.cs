using Innovayse.Hostpanel.Application.Reports.Common.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One group in the SSL Monitoring report.</summary>
/// <param name="Group">Expiry bucket the rows fall into; the panel words it in the operator's language.</param>
/// <param name="Rows">Domains in this bucket.</param>
public record SslMonitoringGroupDto(
    SslExpiryGroup Group,
    IReadOnlyList<SslDomainRowDto> Rows);
