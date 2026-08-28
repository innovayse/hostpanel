namespace Innovayse.Application.Reports.Queries.GetMonthlyOrders;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetMonthlyOrdersQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetMonthlyOrdersHandler(IReportRepository repo)
{
    /// <summary>Returns the orders placed in one month, broken down by product and product group.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Orders grouped by product group, with group and overall totals.</returns>
    public Task<MonthlyOrdersDto> HandleAsync(GetMonthlyOrdersQuery query, CancellationToken ct)
    {
        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same month.
        var now = DateTime.UtcNow;
        return repo.GetMonthlyOrdersAsync(query.Year ?? now.Year, query.Month ?? now.Month, ct);
    }
}
