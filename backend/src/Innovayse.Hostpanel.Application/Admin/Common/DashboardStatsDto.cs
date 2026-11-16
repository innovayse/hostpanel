namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// Aggregated statistics for the admin dashboard.
/// </summary>
/// <param name="ActiveServices">Number of services in Active status.</param>
/// <param name="OverdueInvoices">Number of invoices in Overdue status.</param>
/// <param name="OpenTickets">Number of support tickets in Open status.</param>
/// <param name="TotalClients">Total number of registered client accounts.</param>
public sealed record DashboardStatsDto(
    int ActiveServices,
    int OverdueInvoices,
    int OpenTickets,
    int TotalClients);
