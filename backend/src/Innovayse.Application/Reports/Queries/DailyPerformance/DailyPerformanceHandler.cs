namespace Innovayse.Application.Reports.Queries.DailyPerformance;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="DailyPerformanceQuery"/>.</summary>
public sealed class DailyPerformanceHandler(IReportRepository repo)
{
    /// <summary>Returns daily performance metrics for the given date range.</summary>
    public Task<IReadOnlyList<DailyPerformanceDto>> HandleAsync(DailyPerformanceQuery query, CancellationToken ct)
        => repo.GetDailyPerformanceAsync(query.From, query.To, ct);
}
