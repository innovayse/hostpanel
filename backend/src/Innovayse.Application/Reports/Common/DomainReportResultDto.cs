namespace Innovayse.Application.Reports.Common;

/// <summary>Paginated result for the Domains report.</summary>
public record DomainReportResultDto(IReadOnlyList<DomainReportDto> Items, int TotalCount);
