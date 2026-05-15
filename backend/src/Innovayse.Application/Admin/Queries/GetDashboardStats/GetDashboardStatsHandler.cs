namespace Innovayse.Application.Admin.Queries.GetDashboardStats;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Handles <see cref="GetDashboardStatsQuery"/> by aggregating data from multiple repositories.
/// </summary>
/// <param name="invoiceRepo">Invoice repository for revenue calculations.</param>
/// <param name="serviceRepo">Client service repository for active service count.</param>
/// <param name="ticketRepo">Ticket repository for open ticket count.</param>
/// <param name="clientRepo">Client repository for total client count.</param>
public sealed class GetDashboardStatsHandler(
    IInvoiceRepository invoiceRepo,
    IClientServiceRepository serviceRepo,
    ITicketRepository ticketRepo,
    IClientRepository clientRepo)
{
    /// <summary>
    /// Computes the dashboard statistics.
    /// </summary>
    /// <param name="query">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated dashboard statistics.</returns>
    public async Task<DashboardStatsDto> HandleAsync(GetDashboardStatsQuery query, CancellationToken ct)
    {
        var allInvoices = await invoiceRepo.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var totalRevenue = allInvoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .Sum(i => i.Total);

        var monthlyRevenue = allInvoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= thirtyDaysAgo)
            .Sum(i => i.Total);

        var overdueInvoices = allInvoices.Count(i => i.Status == InvoiceStatus.Overdue);

        var allServices = await serviceRepo.GetAllAsync(ct);
        var activeServices = allServices.Count(s => s.Status == ServiceStatus.Active);

        var openTickets = await ticketRepo.CountByStatusAsync(TicketStatus.Open, ct);
        var totalClients = await clientRepo.CountAllAsync(ct);

        return new DashboardStatsDto(totalRevenue, monthlyRevenue, activeServices, overdueInvoices, openTickets, totalClients);
    }
}
