using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetSalesTaxReport;

/// <summary>Handles <see cref="GetSalesTaxReportQuery"/>.</summary>
/// <remarks>
/// Tax becomes a liability when it is collected, so the period is matched on the day the invoice
/// was paid, not the day it was raised. Amounts in different currencies are never added together:
/// the totals come one per invoice currency, summed in SQL over every matching invoice, and the
/// rows are paged.
/// </remarks>
public sealed class GetSalesTaxReportHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetSalesTaxReportHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The payment days to report on, both bounds optional, and the page of rows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The totals per currency and one page of the paid invoices. Never refuses.</returns>
    public async Task<Result<SalesTaxReportDto>> HandleAsync(GetSalesTaxReportQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var paid = _context.Invoices.AsNoTracking().Where(i => Invoice.PaidStatuses.Contains(i.Status) && i.PaidAt != null);

        if (query.From.HasValue)
        {
            var from = query.From.Value.StartOfDayUtc();
            paid = paid.Where(i => i.PaidAt >= from);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.EndOfDayExclusiveUtc();
            paid = paid.Where(i => i.PaidAt < toExclusive);
        }

        var totals = await paid
            .GroupBy(i => i.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new SalesTaxCurrencyTotalDto(g.Key, g.Count(), g.Sum(i => i.Total), g.Sum(i => i.Tax)))
            .ToListAsync(ct);

        var totalCount = totals.Sum(t => t.TotalInvoices);

        var rows = await paid
            .OrderBy(i => i.PaidAt)
            .ThenBy(i => i.Id)
            .Paginate(query.Page, query.PageSize)
            .Join(_context.Clients.AsNoTracking(), i => i.ClientId, c => c.Id, (i, c) => new SalesTaxRowDto(
                i.Id,
                c.FirstName + " " + c.LastName,
                i.CreatedAt.ToString("yyyy-MM-dd"),
                i.PaidAt!.Value.ToString("yyyy-MM-dd"),
                i.Currency,
                i.SubTotal,
                i.Tax,
                i.Credit,
                i.Total))
            .ToListAsync(ct);

        var page = new PaginatedList<SalesTaxRowDto>(rows, totalCount, query.Page, query.PageSize);
        return Result<SalesTaxReportDto>.Success(new SalesTaxReportDto(totals, page));
    }
}
