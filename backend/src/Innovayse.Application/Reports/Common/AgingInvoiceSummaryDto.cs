namespace Innovayse.Application.Reports.Common;

/// <summary>Full Aging Invoices summary with periods and totals.</summary>
public record AgingInvoiceSummaryDto(
    IReadOnlyList<AgingPeriodDto> Periods,
    Dictionary<string, decimal> Totals,
    IReadOnlyList<string> Currencies);
