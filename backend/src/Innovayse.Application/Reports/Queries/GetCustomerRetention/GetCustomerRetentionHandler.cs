namespace Innovayse.Application.Reports.Queries.GetCustomerRetention;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetCustomerRetentionQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetCustomerRetentionHandler(IReportRepository repo)
{
    /// <summary>Returns average service lifetime grouped by product group.</summary>
    /// <param name="query">Whether still-active services are counted in the average.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Retention figures per product and billing cycle, grouped by product group.</returns>
    public Task<CustomerRetentionDto> HandleAsync(GetCustomerRetentionQuery query, CancellationToken ct)
        => repo.GetCustomerRetentionAsync(query.IncludeActive, ct);
}
