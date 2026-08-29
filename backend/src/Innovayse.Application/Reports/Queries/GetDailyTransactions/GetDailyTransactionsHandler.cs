namespace Innovayse.Application.Reports.Queries.GetDailyTransactions;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetDailyTransactionsQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetDailyTransactionsHandler(IReportRepository repo)
{
    /// <summary>Returns transaction totals for each day of the requested month.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One aggregate per day, with the month's totals.</returns>
    public Task<MonthlyTransactionsReportDto> HandleAsync(
        GetDailyTransactionsQuery query, CancellationToken ct)
    {
        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same month.
        var now = DateTime.UtcNow;
        return repo.GetDailyTransactionsAsync(query.Year ?? now.Year, query.Month ?? now.Month, ct);
    }
}
