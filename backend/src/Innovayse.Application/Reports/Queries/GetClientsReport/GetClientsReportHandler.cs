namespace Innovayse.Application.Reports.Queries.GetClientsReport;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetClientsReportQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetClientsReportHandler(IReportRepository repo)
{
    /// <summary>Returns one page of clients matching the filters.</summary>
    /// <param name="query">Filters and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of clients together with the total row count.</returns>
    public Task<ClientReportResultDto> HandleAsync(GetClientsReportQuery query, CancellationToken ct)
        => repo.GetClientsReportAsync(
            query.Status, query.Country,
            query.CreatedFrom, query.CreatedTo,
            query.Page, query.PageSize, ct);
}
