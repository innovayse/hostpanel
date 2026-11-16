namespace Innovayse.Hostpanel.Application.Reports.Queries.DailyPerformance;

/// <summary>Query for the Daily Performance report.</summary>
/// <param name="From">Start day (inclusive, host time zone), or <see langword="null"/> for 29 days before <see cref="To"/>.</param>
/// <param name="To">End date (inclusive), or <see langword="null"/> for today in the host time zone.</param>
public sealed record DailyPerformanceQuery(DateOnly? From = null, DateOnly? To = null);
