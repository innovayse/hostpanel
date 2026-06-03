namespace Innovayse.Application.Reports.Queries.TransactionsReport;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="TransactionsReportQuery"/>.</summary>
public sealed class TransactionsReportHandler(IReportRepository repo)
{
    /// <summary>Returns filtered transactions report.</summary>
    public Task<TransactionReportResultDto> HandleAsync(TransactionsReportQuery query, CancellationToken ct)
        => repo.GetTransactionsReportAsync(query.DateFrom, query.DateTo, query.PaymentMethod,
            query.Page, query.PageSize, ct);
}
