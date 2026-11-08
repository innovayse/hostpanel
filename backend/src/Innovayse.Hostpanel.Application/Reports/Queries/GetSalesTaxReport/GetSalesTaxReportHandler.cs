using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetSalesTaxReport;

/// <summary>Handles <see cref="GetSalesTaxReportQuery"/>.</summary>
/// <remarks>
/// Tax becomes a liability when it is collected, so the period is matched on the day the invoice
/// was paid, not the day it was raised. Amounts in different currencies are never added together:
/// the totals come one per invoice currency, summed in SQL over every matching invoice, less the tax of the
/// account credit notes issued in the period, and the rows are paged.
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

        var invoiced = await paid
            .GroupBy(i => i.Currency)
            .Select(g => new { Currency = g.Key, Count = g.Count(), Total = g.Sum(i => i.Total), Tax = g.Sum(i => i.Tax) })
            .ToListAsync(ct);

        // A credit note given as account credit reverses part of the invoice's tax without any refund row, so the tax it
        // reversed comes off the liability in the period the note was issued. A gateway refund note is left out: it is
        // one ordinary refund of its invoice. Draft and voided notes reverse nothing.
        var notes = _context.CreditNotes.AsNoTracking().ReversingTax();
        if (query.From.HasValue)
        {
            var from = query.From.Value.StartOfDayUtc();
            notes = notes.Where(n => n.IssuedAt >= from);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.EndOfDayExclusiveUtc();
            notes = notes.Where(n => n.IssuedAt < toExclusive);
        }

        var reversed = await notes
            .GroupBy(n => n.Currency)
            .Select(g => new { Currency = g.Key, Tax = g.Sum(n => n.Tax) })
            .ToListAsync(ct);

        var totals = invoiced.Select(i => i.Currency).Union(reversed.Select(r => r.Currency))
            .OrderBy(currency => currency, StringComparer.Ordinal)
            .Select(currency =>
            {
                var sold = invoiced.FirstOrDefault(i => i.Currency == currency);
                var credited = reversed.FirstOrDefault(r => r.Currency == currency)?.Tax ?? 0m;
                return new SalesTaxCurrencyTotalDto(currency, sold?.Count ?? 0, sold?.Total ?? 0m, (sold?.Tax ?? 0m) - credited);
            })
            .ToList();

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
