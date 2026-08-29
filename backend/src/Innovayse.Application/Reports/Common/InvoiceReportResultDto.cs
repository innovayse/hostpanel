namespace Innovayse.Application.Reports.Common;

/// <summary>Paginated result for the Invoices report.</summary>
public record InvoiceReportResultDto(
    IReadOnlyList<InvoiceReportRowDto> Items,
    int TotalCount);
