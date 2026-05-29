namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Application.Notifications.DTOs;

/// <summary>Invoice count and total for a single status.</summary>
/// <param name="Count">Number of invoices in this status.</param>
/// <param name="Total">Sum of totals for invoices in this status.</param>
public record InvoiceStatusCountDto(int Count, decimal Total);

/// <summary>Aggregated summary data for a client profile dashboard.</summary>
/// <param name="Draft">Draft invoice stats.</param>
/// <param name="Unpaid">Unpaid invoice stats.</param>
/// <param name="Paid">Paid invoice stats.</param>
/// <param name="Overdue">Overdue invoice stats.</param>
/// <param name="Cancelled">Cancelled invoice stats.</param>
/// <param name="Refunded">Refunded invoice stats.</param>
/// <param name="GrossRevenue">Total income from transactions.</param>
/// <param name="ClientExpenses">Total outgoing from transactions.</param>
/// <param name="TotalFees">Total fees from transactions.</param>
/// <param name="NetIncome">Computed: GrossRevenue - ClientExpenses - TotalFees.</param>
/// <param name="CreditBalance">Current credit balance from Client entity.</param>
/// <param name="ActiveServicesCount">Number of services with Active status.</param>
/// <param name="TotalServicesCount">Total number of services.</param>
/// <param name="TotalDomainsCount">Total number of domains.</param>
/// <param name="AcceptedQuotesCount">Number of quotes with Accepted stage.</param>
/// <param name="TotalQuotesCount">Total number of quotes.</param>
/// <param name="OpenTicketsCount">Number of open tickets.</param>
/// <param name="TotalTicketsCount">Total number of tickets.</param>
/// <param name="RecentEmails">Last 5 email log entries sent to this client.</param>
public record ClientSummaryDto(
    InvoiceStatusCountDto Draft,
    InvoiceStatusCountDto Unpaid,
    InvoiceStatusCountDto Paid,
    InvoiceStatusCountDto Overdue,
    InvoiceStatusCountDto Cancelled,
    InvoiceStatusCountDto Refunded,
    decimal GrossRevenue,
    decimal ClientExpenses,
    decimal TotalFees,
    decimal NetIncome,
    decimal CreditBalance,
    int ActiveServicesCount,
    int TotalServicesCount,
    int TotalDomainsCount,
    int AcceptedQuotesCount,
    int TotalQuotesCount,
    int OpenTicketsCount,
    int TotalTicketsCount,
    IReadOnlyList<EmailLogDto> RecentEmails);
