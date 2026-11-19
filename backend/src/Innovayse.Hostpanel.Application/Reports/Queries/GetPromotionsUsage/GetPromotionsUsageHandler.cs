using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetPromotionsUsage;

/// <summary>Handles <see cref="GetPromotionsUsageQuery"/>.</summary>
public sealed class GetPromotionsUsageHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetPromotionsUsageHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Counts the services that carry each promotion code and converts what the active ones renew for to the base currency,
    /// spread over the months of their billing cycles.
    /// </summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The report: one row per code, the most used first, with the base currency its amounts are in. Never refuses.</returns>
    public async Task<Result<PromotionsUsageReportDto>> HandleAsync(
        GetPromotionsUsageQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rates = await _context.GetBaseCurrencyRatesAsync(ct);

        // The database sums per code, currency, cycle and status, so only those few rows come back to be converted.
        var groups = await _context.ClientServices.AsNoTracking()
            .Where(s => s.PromotionCode != null && s.PromotionCode != string.Empty)
            .Join(_context.Clients, s => s.ClientId, c => c.Id, (s, c) => new { Code = s.PromotionCode!, c.Currency, s.BillingCycle, s.Status, s.RecurringAmount })
            .GroupBy(x => new { x.Code, x.Currency, x.BillingCycle, x.Status })
            .Select(g => new { g.Key.Code, g.Key.Currency, g.Key.BillingCycle, g.Key.Status, Amount = g.Sum(x => x.RecurringAmount), Services = g.Count() })
            .ToListAsync(ct);

        IReadOnlyList<PromotionUsageRowDto> rows = [.. groups
            .GroupBy(g => g.Code)
            .Select(code =>
            {
                var active = code.Where(g => g.Status == ServiceStatus.Active)
                    .Select(g => new ServiceRecurringSum(g.Currency, g.BillingCycle, g.Amount, g.Services))
                    .ToList();
                return new PromotionUsageRowDto(
                    code.Key,
                    code.Sum(g => g.Services),
                    active.Sum(s => s.Services),
                    active.MonthlyInBase(rates).RoundedMonthly(rates));
            })
            .OrderByDescending(r => r.Services)
            .ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)];

        return Result<PromotionsUsageReportDto>.Success(new PromotionsUsageReportDto(rates.Currency, rows));
    }
}
