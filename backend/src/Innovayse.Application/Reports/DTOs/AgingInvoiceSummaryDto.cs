namespace Innovayse.Application.Reports.DTOs;

/// <summary>One period row of the Aging Invoices summary report.</summary>
public record AgingPeriodDto(
    string Period,
    Dictionary<string, decimal> AmountsByCurrency);

/// <summary>Full Aging Invoices summary with periods and totals.</summary>
public record AgingInvoiceSummaryDto(
    IReadOnlyList<AgingPeriodDto> Periods,
    Dictionary<string, decimal> Totals,
    IReadOnlyList<string> Currencies);
