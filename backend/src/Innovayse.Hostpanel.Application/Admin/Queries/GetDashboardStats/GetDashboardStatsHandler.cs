using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardStats;

/// <summary>
/// Handles <see cref="GetDashboardStatsQuery"/> by aggregating data from multiple sources.
/// </summary>
public sealed class GetDashboardStatsHandler
{
    /// <summary>
    /// The module's data context, for revenue, active services, open ticket and total client counts.
    /// </summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>How far back income still counts toward the monthly figure.</summary>
    private static readonly TimeSpan MonthlyWindow = TimeSpan.FromDays(30);

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for revenue, active services, open ticket and total client counts.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetDashboardStatsHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Computes the dashboard statistics.
    /// </summary>
    /// <param name="query">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the aggregated statistics. This use case refuses nothing.</returns>
    public async Task<Result<DashboardStatsDto>> HandleAsync(GetDashboardStatsQuery query, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var thirtyDaysAgo = now - MonthlyWindow;

        // Revenue is income as every income report reads it: payments less refunds, net of credit.
        // The database sums per currency; invoices are issued in each client's own currency, so the
        // sums are converted to the base currency before they are added, since adding drams to
        // dollars as if they were the same unit overstates revenue a thousand times.
        var income = await _context.IncomeEntries()
            .GroupBy(e => e.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Total = g.Sum(e => e.Amount),
                Monthly = g.Sum(e => e.At >= thirtyDaysAgo ? e.Amount : 0m),
            })
            .ToListAsync(ct);
        var rates = await _context.GetBaseCurrencyRatesAsync(ct);

        var totalRevenue = income.Sum(t => rates.ToBase(t.Total, t.Currency));
        var monthlyRevenue = income.Sum(t => rates.ToBase(t.Monthly, t.Currency));

        var overdueInvoices = await _context.Invoices
            .AsNoTracking()
            .CountAsync(i => i.Status == InvoiceStatus.Overdue, ct);

        var activeServices = await _context.ClientServices
            .AsNoTracking()
            .CountAsync(s => s.Status == ServiceStatus.Active, ct);

        var openTickets = await _context.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.Open, ct);

        var totalClients = await _context.Clients.AsNoTracking().CountAsync(ct);

        var dto = new DashboardStatsDto(
            totalRevenue, monthlyRevenue, activeServices, overdueInvoices, openTickets, totalClients, rates.Currency);
        return Result<DashboardStatsDto>.Success(dto);
    }
}
