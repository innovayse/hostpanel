namespace Innovayse.Application.Reports.Common;

/// <summary>Paginated result for the Services report.</summary>
public record ServiceReportResultDto(IReadOnlyList<ServiceReportDto> Items, int TotalCount);
