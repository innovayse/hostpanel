namespace Innovayse.Application.Admin.DTOs;

/// <summary>
/// Aggregated statistics for the admin dashboard.
/// </summary>
/// <param name="TotalRevenue">Sum of all paid invoices across all time.</param>
/// <param name="MonthlyRevenue">Sum of paid invoices in the last 30 days.</param>
/// <param name="ActiveServices">Number of services in Active status.</param>
/// <param name="OverdueInvoices">Number of invoices in Overdue status.</param>
/// <param name="OpenTickets">Number of support tickets in Open status.</param>
/// <param name="TotalClients">Total number of registered client accounts.</param>
public record DashboardStatsDto(
    decimal TotalRevenue,
    decimal MonthlyRevenue,
    int ActiveServices,
    int OverdueInvoices,
    int OpenTickets,
    int TotalClients);
