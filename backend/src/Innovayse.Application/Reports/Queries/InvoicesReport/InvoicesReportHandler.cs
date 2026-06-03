namespace Innovayse.Application.Reports.Queries.InvoicesReport;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="InvoicesReportQuery"/>.</summary>
public sealed class InvoicesReportHandler(IReportRepository repo)
{
    /// <summary>Returns filtered invoices report.</summary>
    public Task<InvoiceReportResultDto> HandleAsync(InvoicesReportQuery query, CancellationToken ct)
        => repo.GetInvoicesReportAsync(query.Status, query.CreatedFrom, query.CreatedTo,
            query.DueFrom, query.DueTo, query.PaidFrom, query.PaidTo,
            query.Page, query.PageSize, ct);
}
