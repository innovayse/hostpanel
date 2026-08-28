namespace Innovayse.Application.Reports.Queries.GetIncomeByProductGrouped;

/// <summary>Query for the Income by Product report, grouped by product group, for one month.</summary>
/// <param name="Year">Calendar year, or null for the current UTC year.</param>
/// <param name="Month">Calendar month 1-12, or null for the current UTC month.</param>
public record GetIncomeByProductGroupedQuery(int? Year = null, int? Month = null);
