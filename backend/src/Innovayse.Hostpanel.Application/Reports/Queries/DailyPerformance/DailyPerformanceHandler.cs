using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.DailyPerformance;

/// <summary>Handles <see cref="DailyPerformanceQuery"/>.</summary>
public sealed class DailyPerformanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public DailyPerformanceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The date range to report on; defaults to the trailing 30 days.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per day in the range. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<DailyPerformanceDto>>> HandleAsync(
        DailyPerformanceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var to = query.To ?? DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var from = query.From ?? to.AddDays(-29);

        // The window is half-open, [first day 00:00, the day after the last 00:00), so the last
        // day counts in full and the bounds compare the columns themselves.
        var fromUtc = from.StartOfDayUtc();
        var toUtc = to.EndOfDayExclusiveUtc();

        var newInvoices = await CountByDayAsync(
            _context.Invoices.AsNoTracking().Where(i => i.CreatedAt >= fromUtc && i.CreatedAt < toUtc).Select(i => i.CreatedAt), ct);

        // An invoice counts as paid on the day it was paid, not the day it was raised.
        var paidInvoices = await CountByDayAsync(
            _context.Invoices.AsNoTracking()
                .Where(i => Invoice.PaidStatuses.Contains(i.Status) && i.PaidAt >= fromUtc && i.PaidAt < toUtc)
                .Select(i => i.PaidAt!.Value),
            ct);

        // Only an accepted order is completed; a pending, cancelled or fraud order is not.
        var completedOrders = await CountByDayAsync(
            _context.Orders.AsNoTracking()
                .Where(o => o.Status == OrderStatus.Active && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
                .Select(o => o.CreatedAt),
            ct);

        var cancellations = await CountByDayAsync(
            _context.CancellationRequests.AsNoTracking().Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toUtc).Select(c => c.CreatedAt), ct);

        var openedTickets = await CountByDayAsync(
            _context.Tickets.AsNoTracking().Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc).Select(t => t.CreatedAt), ct);

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i));

        IReadOnlyList<DailyPerformanceDto> rows = days.Select(day => new DailyPerformanceDto(
            Date: day.ToString("yyyy-MM-dd"),
            CompletedOrders: completedOrders.GetValueOrDefault(day),
            NewInvoices: newInvoices.GetValueOrDefault(day),
            PaidInvoices: paidInvoices.GetValueOrDefault(day),
            OpenedTickets: openedTickets.GetValueOrDefault(day),
            // A reply total needs the ticket-replies aggregate this report does not otherwise
            // compute, so it is reported as zero rather than invented.
            TicketReplies: 0,
            CancellationRequests: cancellations.GetValueOrDefault(day)
        )).ToList();

        return Result<IReadOnlyList<DailyPerformanceDto>>.Success(rows);
    }

    /// <summary>Counts moments per UTC calendar day, grouped in the database.</summary>
    /// <param name="moments">The moments to count, already filtered to the window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of each day that has at least one moment.</returns>
    private static async Task<Dictionary<DateOnly, int>> CountByDayAsync(IQueryable<DateTimeOffset> moments, CancellationToken ct)
    {
        var counts = await moments
            .GroupBy(m => new { m.Year, m.Month, m.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(c => new DateOnly(c.Year, c.Month, c.Day), c => c.Count);
    }
}
