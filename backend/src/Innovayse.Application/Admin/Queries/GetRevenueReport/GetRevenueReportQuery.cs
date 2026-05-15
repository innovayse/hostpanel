namespace Innovayse.Application.Admin.Queries.GetRevenueReport;

/// <summary>Query that returns daily revenue totals within a date range.</summary>
/// <param name="StartDate">Start of the reporting period (inclusive, UTC).</param>
/// <param name="EndDate">End of the reporting period (inclusive, UTC).</param>
public record GetRevenueReportQuery(DateTimeOffset StartDate, DateTimeOffset EndDate);
