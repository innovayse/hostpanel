namespace Innovayse.Application.Admin.Queries.GetRevenueReport;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Handles <see cref="GetRevenueReportQuery"/> by grouping paid invoices by payment date.
/// </summary>
/// <param name="invoiceRepo">Invoice repository.</param>
public sealed class GetRevenueReportHandler(IInvoiceRepository invoiceRepo)
{
    /// <summary>
    /// Returns daily revenue totals for the requested date range.
    /// </summary>
    /// <param name="query">The revenue report query with start and end dates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of daily revenue items ordered by date.</returns>
    public async Task<IReadOnlyList<RevenueReportItemDto>> HandleAsync(
        GetRevenueReportQuery query, CancellationToken ct)
    {
        var invoices = await invoiceRepo.GetPaidBetweenAsync(query.StartDate, query.EndDate, ct);

        return invoices
            .GroupBy(i => DateOnly.FromDateTime(i.PaidAt!.Value.UtcDateTime))
            .Select(g => new RevenueReportItemDto(g.Key, g.Sum(i => i.Total)))
            .OrderBy(r => r.Date)
            .ToList();
    }
}
