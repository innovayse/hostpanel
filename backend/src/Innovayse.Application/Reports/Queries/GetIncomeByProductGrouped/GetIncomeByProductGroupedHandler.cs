namespace Innovayse.Application.Reports.Queries.GetIncomeByProductGrouped;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetIncomeByProductGroupedQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetIncomeByProductGroupedHandler(IReportRepository repo)
{
    /// <summary>Returns income for one month, broken down by product and product group.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Income grouped by product group, with group and overall totals.</returns>
    public Task<IncomeByProductGroupedDto> HandleAsync(
        GetIncomeByProductGroupedQuery query, CancellationToken ct)
    {
        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same month.
        var now = DateTime.UtcNow;
        return repo.GetIncomeByProductGroupedAsync(query.Year ?? now.Year, query.Month ?? now.Month, ct);
    }
}
