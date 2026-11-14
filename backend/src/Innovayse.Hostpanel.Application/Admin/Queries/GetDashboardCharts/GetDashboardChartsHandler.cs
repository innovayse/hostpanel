using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
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
            .CountPerDayAsync(window.Current, ct);

        var newClients = await _context.Clients
            .AsNoTracking()
            .Select(c => c.CreatedAt)
            .CountPerDayAsync(window.Current, ct);

        var invoices = await ReadInvoicesByStatusAsync(window.Current, rates, ct);
        var tickets = await ReadOpenTicketsAsync(ct);

        return Result<DashboardChartsDto>.Success(new DashboardChartsDto(
            rates.Currency,
            income,
            ToDailyCounts(window.Current, newOrders),
            ToDailyCounts(window.Current, newClients),
            invoices,
            tickets));
    }

    /// <summary>Adds income up in the base currency: each currency summed first, then converted once.</summary>
    /// <param name="entries">The income to add up.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <returns>The sum in the base currency.</returns>
    private static decimal ToBase(IEnumerable<IncomeDay> entries, BaseCurrencyRates rates) =>
        entries.GroupBy(e => e.Currency).Sum(currency => rates.ToBase(currency.Sum(e => e.Amount), currency.Key));

    /// <summary>Lays counted days out as one entry per day of the window, a quiet day answering zero.</summary>
    /// <param name="window">The days to lay out.</param>
    /// <param name="counts">The count of each day that has any.</param>
    /// <returns>One count per day of the window, oldest first.</returns>
    private static IReadOnlyList<DashboardDailyCountDto> ToDailyCounts(DayWindow window, IReadOnlyDictionary<DateOnly, int> counts) =>
        [.. window.Days.Select(day => new DashboardDailyCountDto(day, counts.GetValueOrDefault(day)))];

    /// <summary>Reads income per day, its total and the total of the period before, as every income report reads it.</summary>
    /// <param name="window">The periods to read.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The income series.</returns>
    private async Task<DashboardIncomeDto> ReadIncomeAsync(DashboardWindow window, BaseCurrencyRates rates, CancellationToken ct)
    {
        // The money is in each invoice's own currency, so it is summed per currency first and converted after:
        // adding drams to dollars never happens. One read serves both periods.
        var entries = await _context.IncomeEntries().SumPerDayAsync(window.Span, ct);
        var current = entries.Where(e => e.Day >= window.Current.First).ToList();
        var previous = entries.Where(e => e.Day < window.Current.First).ToList();

        // A total is the sum per currency converted once, for the period and for the one before alike, so the two
        // are rounded the same way and the change between them is honest. The chart's days are converted one by one.
        var total = ToBase(current, rates);
        var previousTotal = ToBase(previous, rates);
        var perDay = current.GroupBy(e => e.Day).ToDictionary(day => day.Key, day => ToBase(day, rates));
        IReadOnlyList<DashboardDailyAmountDto> days =
            [.. window.Current.Days.Select(day => new DashboardDailyAmountDto(day, perDay.GetValueOrDefault(day)))];

        decimal? change = previousTotal > 0m
            ? Math.Round((total - previousTotal) / previousTotal * PercentScale, PercentDecimals)
            : null;

        return new DashboardIncomeDto(days, total, previousTotal, change, total / window.Current.Days.Count);
    }

    /// <summary>Reads the invoices raised in the period, counted and added up per charted status.</summary>
    /// <param name="window">The days to read.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per charted status, in display order, zero when none stands in it.</returns>
    private async Task<IReadOnlyList<DashboardInvoiceStatusDto>> ReadInvoicesByStatusAsync(
        DayWindow window, BaseCurrencyRates rates, CancellationToken ct)
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
            return new DashboardInvoiceStatusDto(status, rows.Sum(r => r.Count), rows.GroupBy(r => r.Currency).Sum(c => rates.ToBase(c.Sum(r => r.Total), c.Key)));
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
