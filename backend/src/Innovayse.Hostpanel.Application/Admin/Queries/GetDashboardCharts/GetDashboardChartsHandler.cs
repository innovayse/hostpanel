using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Extensions;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardCharts;

/// <summary>Handles <see cref="GetDashboardChartsQuery"/> by reading each series the dashboard charts draw.</summary>
public sealed class GetDashboardChartsHandler
{
    /// <summary>The invoice statuses the dashboard charts, in the order they are shown.</summary>
    private static readonly InvoiceStatus[] ChartedInvoiceStatuses =
        [InvoiceStatus.Paid, InvoiceStatus.Unpaid, InvoiceStatus.Overdue, InvoiceStatus.Cancelled];

    /// <summary>Scale of a change expressed in percent.</summary>
    private const decimal PercentScale = 100m;

    /// <summary>Decimal places a percentage change is rounded to.</summary>
    private const int PercentDecimals = 1;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment and of the host time zone.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment and of the host time zone.</param>
    public GetDashboardChartsHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Reads the series for the period.</summary>
    /// <param name="query">The period to cover.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the series, every amount in the base currency. This use case refuses nothing.</returns>
    public async Task<Result<DashboardChartsDto>> HandleAsync(GetDashboardChartsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var window = DashboardWindow.Create(query.Period, _clock.UtcNow, _clock.TimeZone);
        var rates = await _context.GetBaseCurrencyRatesAsync(ct);

        var income = await ReadIncomeAsync(window, rates, ct);

        // Orders count as the monthly orders report counts them: a cancelled or fraud order sold nothing.
        var newOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Fraud)
            .Select(o => o.CreatedAt)
            .CountPerDayAsync(window, ct);

        var newClients = await _context.Clients
            .AsNoTracking()
            .Select(c => c.CreatedAt)
            .CountPerDayAsync(window, ct);

        var invoices = await ReadInvoicesByStatusAsync(window, rates, ct);
        var tickets = await ReadOpenTicketsAsync(ct);

        return Result<DashboardChartsDto>.Success(new DashboardChartsDto(rates.Currency, income, newOrders, newClients, invoices, tickets));
    }

    /// <summary>Reads income per day, its total and the total of the period before, as every income report reads it.</summary>
    /// <param name="window">The days to read.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The income series.</returns>
    private async Task<DashboardIncomeDto> ReadIncomeAsync(DashboardWindow window, BaseCurrencyRates rates, CancellationToken ct)
    {
        // The money is in each invoice's own currency: it is summed per currency first and converted after,
        // so adding drams to dollars never happens.
        var entries = await _context.IncomeEntries()
            .Within(window.PreviousFrom, window.To)
            .Select(e => new { e.At, e.Currency, e.Amount })
            .ToListAsync(ct);

        var perDay = entries
            .Where(e => e.At >= window.From)
            .GroupBy(e => window.DayOf(e.At))
            .ToDictionary(
                day => day.Key,
                day => day.GroupBy(e => e.Currency).Sum(c => rates.ToBase(c.Sum(e => e.Amount), c.Key)));
        IReadOnlyList<DashboardDailyAmountDto> days = [.. window.Days.Select(day => new DashboardDailyAmountDto(day, perDay.GetValueOrDefault(day)))];

        var total = days.Sum(d => d.Amount);
        var previousTotal = entries
            .Where(e => e.At < window.From)
            .GroupBy(e => e.Currency)
            .Sum(c => rates.ToBase(c.Sum(e => e.Amount), c.Key));
        decimal? change = previousTotal > 0m
            ? Math.Round((total - previousTotal) / previousTotal * PercentScale, PercentDecimals)
            : null;

        return new DashboardIncomeDto(days, total, previousTotal, change, total / window.Days.Count);
    }

    /// <summary>Reads the invoices raised in the period, counted and added up per charted status.</summary>
    /// <param name="window">The days to read.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per charted status, in display order, zero when none stands in it.</returns>
    private async Task<IReadOnlyList<DashboardInvoiceStatusDto>> ReadInvoicesByStatusAsync(
        DashboardWindow window, BaseCurrencyRates rates, CancellationToken ct)
    {
        var sums = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= window.From && i.CreatedAt < window.To)
            .GroupBy(i => new { i.Status, i.Currency })
            .Select(g => new { g.Key.Status, g.Key.Currency, Count = g.Count(), Total = g.Sum(i => i.Total) })
            .ToListAsync(ct);

        // A partially refunded invoice was paid, the same rule every income and report filter reads.
        return [.. ChartedInvoiceStatuses.Select(status =>
        {
            var rows = sums.Where(s => status == InvoiceStatus.Paid ? Invoice.PaidStatuses.Contains(s.Status) : s.Status == status).ToList();
            return new DashboardInvoiceStatusDto(status, rows.Sum(r => r.Count), rows.Sum(r => rates.ToBase(r.Total, r.Currency)));
        })];
    }

    /// <summary>Counts the open tickets of each priority.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per priority, the highest first, zero when none is open.</returns>
    private async Task<IReadOnlyList<DashboardTicketPriorityDto>> ReadOpenTicketsAsync(CancellationToken ct)
    {
        var counts = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Open)
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return [.. Enum.GetValues<TicketPriority>()
            .OrderByDescending(priority => priority)
            .Select(priority => new DashboardTicketPriorityDto(priority, counts.FirstOrDefault(c => c.Priority == priority)?.Count ?? 0))];
    }
}
