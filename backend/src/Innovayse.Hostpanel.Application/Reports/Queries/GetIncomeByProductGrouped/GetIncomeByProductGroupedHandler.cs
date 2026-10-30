using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetIncomeByProductGrouped;

/// <summary>Handles <see cref="GetIncomeByProductGroupedQuery"/>.</summary>
public sealed class GetIncomeByProductGroupedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetIncomeByProductGroupedHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The month to report on; defaults to the current UTC year and month.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Income of the invoices paid in the month, by product through the services the lines bill,
    /// grouped by active product group and converted to the base currency. Never refuses.
    /// </returns>
    public async Task<Result<IncomeByProductGroupedDto>> HandleAsync(
        GetIncomeByProductGroupedQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = ReportMonth.Resolve(query.Year, query.Month, _clock.UtcNow);
        var year = period.Year;
        var month = period.Month;
        var from = period.From;
        var to = period.To;

        // The invoices paid this month, by the moment they were paid. Each service line is valued by
        // the invoice's own ShareOfTotal rule, so tax is included, credit (never the payer's money)
        // is left out and the shares are rounded to the currency's minor unit like the invoice is.
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Where(i => Invoice.PaidStatuses.Contains(i.Status) && i.PaidAt >= from && i.PaidAt < to)
            .ToListAsync(ct);

        var lines = invoices
            .SelectMany(invoice => invoice.Items
                .Where(item => item.ClientServiceId != null)
                .Select(item => new { Invoice = invoice, Item = item, ServiceId = item.ClientServiceId!.Value }))
            .ToList();

        // A line names the service it bills; the service names the product. A line that bills no
        // service (a domain, a manual charge) belongs to no product and is not attributed.
        var serviceIds = lines.Select(l => l.ServiceId).Distinct().ToList();
        var productOfService = await _context.ClientServices
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .Select(s => new { s.Id, s.ProductId })
            .ToDictionaryAsync(s => s.Id, s => s.ProductId, ct);
        var rates = await _context.GetBaseCurrencyRatesAsync(ct);

        var incomeMap = lines
            .Where(l => productOfService.ContainsKey(l.ServiceId))
            .GroupBy(l => productOfService[l.ServiceId])
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Units = g.Sum(l => l.Item.Quantity),
                    Income = g.Sum(l => rates.ToBase(l.Invoice.ShareOfTotal(l.Item.Amount), l.Invoice.Currency)),
                });

        var groups = await _context.ProductGroups
            .AsNoTracking()
            .Where(g => g.IsActive)
            .Include(g => g.Products.Where(p => p.Status == ProductStatus.Active))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        var result = groups.Select(g =>
        {
            var products = g.Products.Select(p =>
            {
                var sale = incomeMap.GetValueOrDefault(p.Id);
                return new IncomeByProductRowDto(p.Id, p.Name, sale?.Units ?? 0, sale?.Income ?? 0m);
            }).ToList();

            return new IncomeByProductGroupDto(
                g.Name,
                products,
                products.Sum(p => p.UnitsSold),
                products.Sum(p => p.TotalIncome));
        }).ToList();

        var dto = new IncomeByProductGroupedDto(
            month, year, result,
            result.Sum(g => g.GroupUnitsSold),
            result.Sum(g => g.GroupIncome),
            rates.Currency);

        return Result<IncomeByProductGroupedDto>.Success(dto);
    }
}
