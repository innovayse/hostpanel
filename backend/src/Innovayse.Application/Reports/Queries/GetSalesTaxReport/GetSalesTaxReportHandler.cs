namespace Innovayse.Application.Reports.Queries.GetSalesTaxReport;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetSalesTaxReportQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetSalesTaxReportHandler(IReportRepository repo)
{
    /// <summary>Returns the sales tax collected over the requested date range.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tax liability broken down as the report presents it.</returns>
    public Task<SalesTaxReportDto> HandleAsync(GetSalesTaxReportQuery query, CancellationToken ct)
        => repo.GetSalesTaxReportAsync(query.From, query.To, ct);
}
