namespace Innovayse.Application.Reports.Common;

/// <summary>One period row of the Aging Invoices summary report.</summary>
public record AgingPeriodDto(
    string Period,
    Dictionary<string, decimal> AmountsByCurrency);
