namespace Innovayse.Application.Reports.Common;

/// <summary>Paginated result wrapper for the Clients report.</summary>
public record ClientReportResultDto(IReadOnlyList<ClientReportDto> Items, int TotalCount);
