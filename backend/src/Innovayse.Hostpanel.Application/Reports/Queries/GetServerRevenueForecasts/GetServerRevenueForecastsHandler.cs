using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetServerRevenueForecasts;

/// <summary>Handles <see cref="GetServerRevenueForecastsQuery"/>.</summary>
public sealed class GetServerRevenueForecastsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetServerRevenueForecastsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Projects what the active services provisioned on each server renew for, converted to the base currency and spread over
    /// the months of their billing cycles.
    /// </summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The report: one row per server, the best earning first (a server without active services shows zero), with the base currency its amounts are in. Never refuses.</returns>
    public async Task<Result<ServerRevenueForecastsReportDto>> HandleAsync(
        GetServerRevenueForecastsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rates = await _context.GetBaseCurrencyRatesAsync(ct);
        var servers = await _context.Servers.AsNoTracking()
            .Select(s => new { s.Id, s.Name, s.Hostname })
            .ToListAsync(ct);

        var revenue = await _context.GetServerRevenueAsync(rates, null, ct);

        IReadOnlyList<ServerRevenueForecastRowDto> rows = [.. servers
            .Select(server =>
            {
                var served = revenue.GetValueOrDefault(server.Id, new ServerRevenue(0, 0m));
                return new ServerRevenueForecastRowDto(
                    server.Id,
                    server.Name,
                    server.Hostname,
                    served.ActiveServices,
                    served.Monthly.RoundedMonthly(rates),
                    served.Monthly.RoundedAnnual(rates));
            })
            .OrderByDescending(r => r.MonthlyRevenue)
            .ThenBy(r => r.ServerName, StringComparer.OrdinalIgnoreCase)];

        return Result<ServerRevenueForecastsReportDto>.Success(new ServerRevenueForecastsReportDto(rates.Currency, rows));
    }
}
