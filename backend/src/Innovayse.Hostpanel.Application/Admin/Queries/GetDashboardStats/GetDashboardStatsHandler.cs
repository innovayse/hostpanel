using Innovayse.Hostpanel.Application.Admin.Common;
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
    /// The module's data context, for the overdue invoice, active service, open ticket and total client counts.
    /// </summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the overdue invoice, active service, open ticket and total client counts.</param>
    public GetDashboardStatsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Computes the dashboard statistics.
    /// </summary>
    /// <param name="query">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the aggregated statistics. This use case refuses nothing.</returns>
    public async Task<Result<DashboardStatsDto>> HandleAsync(GetDashboardStatsQuery query, CancellationToken ct)
    {
        var overdueInvoices = await _context.Invoices
            .AsNoTracking()
            .CountAsync(i => i.Status == InvoiceStatus.Overdue, ct);

        var activeServices = await _context.ClientServices
            .AsNoTracking()
            .CountAsync(s => s.Status == ServiceStatus.Active, ct);

        var openTickets = await _context.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.Open, ct);

        var totalClients = await _context.Clients.AsNoTracking().CountAsync(ct);

        var dto = new DashboardStatsDto(activeServices, overdueInvoices, openTickets, totalClients);
        return Result<DashboardStatsDto>.Success(dto);
    }
}
