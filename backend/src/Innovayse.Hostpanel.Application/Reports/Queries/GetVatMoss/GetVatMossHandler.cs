using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetVatMoss;

/// <summary>Handles <see cref="GetVatMossQuery"/>.</summary>
/// <remarks>
/// A quarter is the half-open UTC window <c>[first day, first day of the next quarter)</c>. Sales
/// are the taxed invoices paid in it, summed per member state, VAT rate and invoice currency in
/// SQL. Refunds are booked in the quarter they were made, not the quarter of the sale: each refund
/// made in the window is split into its net and VAT parts in the proportion of the invoice's
/// subtotal and tax, and subtracted from its row, so a partial refund reduces the figures by only
/// what it gave back. A Refunded invoice with no refund recorded here (imported history) took and
/// returned its money elsewhere, so it is not counted as a sale.
/// </remarks>
public sealed class GetVatMossHandler
{
    /// <summary>Calendar months in a quarter.</summary>
    private const int MonthsPerQuarter = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetVatMossHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">Year and quarter to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>VAT collected per member state, rate and currency for the quarter, net of the refunds made in it. Never refuses.</returns>
    public async Task<Result<VatMossDto>> HandleAsync(GetVatMossQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same quarter.
        var now = _clock.UtcNow.UtcDateTime;
        var year = query.Year ?? now.Year;
        var quarter = query.Quarter ?? (((now.Month - 1) / MonthsPerQuarter) + 1);

        var periodStart = new DateOnly(year, ((quarter - 1) * MonthsPerQuarter) + 1, 1);
        var nextPeriodStart = periodStart.AddMonths(MonthsPerQuarter);
        var from = periodStart.StartOfDayUtc();
        var until = nextPeriodStart.StartOfDayUtc();
        var memberStates = EuMemberStates.Codes;

        var sales = await _context.Invoices.AsNoTracking()
            .Where(i => (Invoice.PaidStatuses.Contains(i.Status)
                    || (i.Status == InvoiceStatus.Refunded && i.Transactions.Any(t => t.Type == InvoiceTransactionType.Refund)))
                && i.Tax > 0
                && i.PaidAt >= from
                && i.PaidAt < until)
            .Join(_context.Clients.AsNoTracking(), i => i.ClientId, c => c.Id,
                (i, c) => new { c.Country, i.TaxRate, i.Currency, i.SubTotal, i.Tax })
            .Where(x => x.Country != null && memberStates.Contains(x.Country))
            .GroupBy(x => new { x.Country, x.TaxRate, x.Currency })
            .Select(g => new
            {
                Country = g.Key.Country!,
                g.Key.TaxRate,
                g.Key.Currency,
                Invoices = g.Count(),
                Net = g.Sum(x => x.SubTotal),
                Vat = g.Sum(x => x.Tax),
            })
            .ToListAsync(ct);

        var refunds = await _context.InvoiceTransactions.AsNoTracking()
            .Where(t => t.Type == InvoiceTransactionType.Refund && t.Date >= from && t.Date < until)
            .Join(_context.Invoices.AsNoTracking(), t => t.InvoiceId, i => i.Id,
                (t, i) => new { t.Amount, i.ClientId, i.TaxRate, i.Currency, i.SubTotal, i.Tax })
            .Where(x => x.Tax > 0)
            .Join(_context.Clients.AsNoTracking(), x => x.ClientId, c => c.Id,
                (x, c) => new { c.Country, x.TaxRate, x.Currency, x.Amount, x.SubTotal, x.Tax })
            .Where(x => x.Country != null && memberStates.Contains(x.Country))
            .ToListAsync(ct);

        // Each refund gives back VAT in the proportion the invoice charged it.
        var refundParts = refunds
            .Select(r =>
            {
                var vat = CurrencyCode.RoundToMinorUnits(r.Amount * r.Tax / (r.SubTotal + r.Tax), r.Currency);
                return new { Country = r.Country!, r.TaxRate, r.Currency, Net = r.Amount - vat, Vat = vat };
            })
            .ToList();

        var rows = sales
            .Select(s => new { s.Country, s.TaxRate, s.Currency })
            .Concat(refundParts.Select(r => new { r.Country, r.TaxRate, r.Currency }))
            .Distinct()
            .Select(key =>
            {
                var sold = sales.Where(s => s.Country == key.Country && s.TaxRate == key.TaxRate && s.Currency == key.Currency).ToList();
                var given = refundParts.Where(r => r.Country == key.Country && r.TaxRate == key.TaxRate && r.Currency == key.Currency).ToList();
                return new VatMossRowDto(
                    key.Country,
                    key.TaxRate,
                    sold.Sum(s => s.Invoices),
                    sold.Sum(s => s.Net) - given.Sum(r => r.Net),
                    sold.Sum(s => s.Vat) - given.Sum(r => r.Vat),
                    key.Currency);
            })
            .OrderBy(r => r.CountryCode, StringComparer.Ordinal)
            .ThenBy(r => r.VatRate)
            .ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();

        return Result<VatMossDto>.Success(new VatMossDto(periodStart, nextPeriodStart.AddDays(-1), rows));
    }
}
