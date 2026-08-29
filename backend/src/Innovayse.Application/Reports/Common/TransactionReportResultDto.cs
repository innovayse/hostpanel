namespace Innovayse.Application.Reports.Common;

/// <summary>Paginated result for the Transactions report.</summary>
public record TransactionReportResultDto(
    IReadOnlyList<TransactionReportRowDto> Items,
    int TotalCount);
