namespace Innovayse.Application.Admin.DTOs;

/// <summary>
/// A single data point in the revenue report, representing one day's income.
/// </summary>
/// <param name="Date">The calendar date for this data point.</param>
/// <param name="Revenue">Total revenue collected on this date.</param>
public record RevenueReportItemDto(DateOnly Date, decimal Revenue);
