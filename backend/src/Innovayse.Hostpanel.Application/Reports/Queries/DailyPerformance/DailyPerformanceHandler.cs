using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
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

    /// <summary>The source of the current moment and of the host time zone.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment and of the host time zone.</param>
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

        // Days are the host time zone's, the same as on the dashboard: the window is half-open, [first day 00:00,
        // the day after the last 00:00), so the last day counts in full. The count applies the window's bounds itself.
        var to = query.To ?? DayWindow.DayIn(_clock.UtcNow, _clock.TimeZone);
        var window = DayWindow.Create(query.From ?? to.AddDays(-29), to, _clock.TimeZone);

        var newInvoices = await _context.Invoices.AsNoTracking().Select(i => i.CreatedAt).CountPerDayAsync(window, ct);

        // An invoice counts as paid on the day it was paid, not the day it was raised.
        var paidInvoices = await _context.Invoices.AsNoTracking()
            .Where(i => Invoice.PaidStatuses.Contains(i.Status) && i.PaidAt != null)
            .Select(i => i.PaidAt!.Value)
            .CountPerDayAsync(window, ct);

        // Only an accepted order is completed; a pending, cancelled or fraud order is not. An accepted order
        // with a failed line is still accepted (its client was charged and its other lines delivered).
        var completedOrders = await _context.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Active || o.Status == OrderStatus.Incomplete)
            .Select(o => o.CreatedAt)
            .CountPerDayAsync(window, ct);

        var cancellations = await _context.CancellationRequests.AsNoTracking().Select(c => c.CreatedAt).CountPerDayAsync(window, ct);

        var openedTickets = await _context.Tickets.AsNoTracking().Select(t => t.CreatedAt).CountPerDayAsync(window, ct);

        IReadOnlyList<DailyPerformanceDto> rows = window.Days.Select(day => new DailyPerformanceDto(
            Date: day.ToString("yyyy-MM-dd"),
            CompletedOrders: completedOrders.GetValueOrDefault(day),
            NewInvoices: newInvoices.GetValueOrDefault(day),
            PaidInvoices: paidInvoices.GetValueOrDefault(day),
            OpenedTickets: openedTickets.GetValueOrDefault(day),
            // A reply total needs the ticket-replies aggregate this report does not otherwise
            // compute, so it is reported as zero rather than invented.
            TicketReplies: 0,
            CancellationRequests: cancellations.GetValueOrDefault(day))).ToList();

        return Result<IReadOnlyList<DailyPerformanceDto>>.Success(rows);
    }
}
