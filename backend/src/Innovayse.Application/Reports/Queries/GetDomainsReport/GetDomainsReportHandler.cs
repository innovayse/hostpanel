namespace Innovayse.Application.Reports.Queries.GetDomainsReport;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetDomainsReportQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetDomainsReportHandler(IReportRepository repo)
{
    /// <summary>Returns one page of domains matching the filters.</summary>
    /// <param name="query">Filters and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of domains together with the total row count.</returns>
    public Task<DomainReportResultDto> HandleAsync(GetDomainsReportQuery query, CancellationToken ct)
        => repo.GetDomainsReportAsync(
            query.Status, query.Registrar,
            query.RegisteredFrom, query.RegisteredTo,
            query.ExpiresFrom, query.ExpiresTo,
            query.NextDueFrom, query.NextDueTo,
            query.Page, query.PageSize, ct);
}
