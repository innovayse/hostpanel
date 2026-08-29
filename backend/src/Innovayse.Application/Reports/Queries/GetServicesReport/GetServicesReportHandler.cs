namespace Innovayse.Application.Reports.Queries.GetServicesReport;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetServicesReportQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetServicesReportHandler(IReportRepository repo)
{
    /// <summary>Returns one page of services matching the filters.</summary>
    /// <param name="query">Filters and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of services together with the total row count.</returns>
    public Task<ServiceReportResultDto> HandleAsync(GetServicesReportQuery query, CancellationToken ct)
        => repo.GetServicesReportAsync(
            query.Status, query.BillingCycle,
            query.CreatedFrom, query.CreatedTo,
            query.NextDueFrom, query.NextDueTo,
            query.TerminatedFrom, query.TerminatedTo,
            query.Page, query.PageSize, ct);
}
