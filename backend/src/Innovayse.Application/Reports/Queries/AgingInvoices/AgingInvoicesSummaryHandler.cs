namespace Innovayse.Application.Reports.Queries.AgingInvoices;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="AgingInvoicesSummaryQuery"/>.</summary>
public sealed class AgingInvoicesSummaryHandler(IReportRepository repo)
{
    /// <summary>Returns aging invoices summary grouped by period and currency.</summary>
    public Task<AgingInvoiceSummaryDto> HandleAsync(AgingInvoicesSummaryQuery query, CancellationToken ct)
        => repo.GetAgingInvoicesSummaryAsync(ct);
}
