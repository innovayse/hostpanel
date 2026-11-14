namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>Everything the dashboard charts draw for one period.</summary>
/// <param name="Currency">ISO 4217 code of the base currency every amount is in.</param>
/// <param name="Income">Income per day with its total and change against the period before.</param>
/// <param name="NewOrders">Orders placed per day, a cancelled or fraud order excluded, every day present.</param>
/// <param name="NewClients">Client accounts created per day, every day present.</param>
/// <param name="InvoicesByStatus">Paid, unpaid, overdue and cancelled invoices raised in the period, in that order.</param>
/// <param name="OpenTickets">Open tickets by priority, every priority present, the highest first.</param>
public sealed record DashboardChartsDto(
    string Currency,
    DashboardIncomeDto Income,
    IReadOnlyList<DashboardDailyCountDto> NewOrders,
    IReadOnlyList<DashboardDailyCountDto> NewClients,
    IReadOnlyList<DashboardInvoiceStatusDto> InvoicesByStatus,
    IReadOnlyList<DashboardTicketPriorityDto> OpenTickets);
