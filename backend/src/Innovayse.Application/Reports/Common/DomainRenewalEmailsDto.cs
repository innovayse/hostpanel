namespace Innovayse.Application.Reports.Common;

/// <summary>Domain renewal reminder emails report.</summary>
public record DomainRenewalEmailsDto(IReadOnlyList<DomainRenewalEmailRowDto> Rows);
