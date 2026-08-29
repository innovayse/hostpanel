namespace Innovayse.Application.Reports.Queries.AgingInvoices;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="AgingInvoicesQuery"/>.</summary>
public sealed class AgingInvoicesHandler(IReportRepository repo)
{
    /// <summary>Returns unpaid invoices with aging buckets.</summary>
    public Task<IReadOnlyList<AgingInvoiceDto>> HandleAsync(AgingInvoicesQuery query, CancellationToken ct)
        => repo.GetAgingInvoicesAsync(ct);
}
