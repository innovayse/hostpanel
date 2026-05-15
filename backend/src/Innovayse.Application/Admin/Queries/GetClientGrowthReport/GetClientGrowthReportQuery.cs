namespace Innovayse.Application.Admin.Queries.GetClientGrowthReport;

/// <summary>Query that returns daily new client registration counts within a date range.</summary>
/// <param name="StartDate">Start of the reporting period (inclusive, UTC).</param>
/// <param name="EndDate">End of the reporting period (inclusive, UTC).</param>
public record GetClientGrowthReportQuery(DateTimeOffset StartDate, DateTimeOffset EndDate);
