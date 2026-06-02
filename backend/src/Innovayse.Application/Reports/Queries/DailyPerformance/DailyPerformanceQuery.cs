namespace Innovayse.Application.Reports.Queries.DailyPerformance;

/// <summary>Query for the Daily Performance report.</summary>
/// <param name="From">Start date (inclusive).</param>
/// <param name="To">End date (inclusive).</param>
public record DailyPerformanceQuery(DateOnly From, DateOnly To);
