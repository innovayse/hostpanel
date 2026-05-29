namespace Innovayse.Application.Clients.Queries.GetClientSummary;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Notifications.DTOs;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Handles <see cref="GetClientSummaryQuery"/> by aggregating data from multiple repositories
/// and returning a <see cref="ClientSummaryDto"/> for the client dashboard.
/// </summary>
/// <param name="invoiceRepo">Invoice repository for invoice stats.</param>
/// <param name="transactionRepo">Client transaction repository for financial summary.</param>
/// <param name="clientRepo">Client repository for credit balance lookup.</param>
/// <param name="serviceRepo">Client service repository for service counts.</param>
/// <param name="domainRepo">Domain repository for domain counts.</param>
/// <param name="quoteRepo">Quote repository for quote counts.</param>
/// <param name="ticketRepo">Ticket repository for ticket counts.</param>
/// <param name="emailLogRepo">Email log repository for recent emails.</param>
public sealed class GetClientSummaryHandler(
    IInvoiceRepository invoiceRepo,
    ITransactionRepository transactionRepo,
    IClientRepository clientRepo,
    IClientServiceRepository serviceRepo,
    IDomainRepository domainRepo,
    IQuoteRepository quoteRepo,
    ITicketRepository ticketRepo,
    IEmailLogRepository emailLogRepo)
{
    /// <summary>
    /// Executes the query by fetching data from all repositories sequentially
    /// and composing the aggregated summary.
    /// </summary>
    /// <param name="query">The query containing the client ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated client summary including invoice stats, income, counts, and recent emails.</returns>
    public async Task<ClientSummaryDto> HandleAsync(GetClientSummaryQuery query, CancellationToken ct)
    {
        var clientId = query.ClientId;

        // EF Core DbContext is not thread-safe — queries must run sequentially.
        var invoices = await invoiceRepo.ListByClientAsync(clientId, ct);
        var (totalIn, totalOut, totalFees) = await transactionRepo.GetClientSummaryAsync(clientId, ct);
        var client = await clientRepo.FindByIdAsync(clientId, ct);
        var services = await serviceRepo.ListByClientAsync(clientId, ct);
        var domains = await domainRepo.ListByClientAsync(clientId, ct);
        var (quotes, _) = await quoteRepo.ListByClientAsync(clientId, 1, int.MaxValue, ct);
        var tickets = await ticketRepo.ListByClientIdAsync(clientId, ct);
        var (emailLogs, _) = await emailLogRepo.ListByClientIdAsync(clientId, 1, 5, ct);

        var invoicesByStatus = invoices
            .GroupBy(i => i.Status)
            .ToDictionary(g => g.Key, g => new InvoiceStatusCountDto(g.Count(), g.Sum(i => i.Total)));

        var recentEmails = emailLogs
            .Select(e => new EmailLogDto(e.Id, e.To, e.Subject, e.SentAt, e.Success, e.Error))
            .ToList();

        return new ClientSummaryDto(
            Draft: GetStatusCount(invoicesByStatus, InvoiceStatus.Draft),
            Unpaid: GetStatusCount(invoicesByStatus, InvoiceStatus.Unpaid),
            Paid: GetStatusCount(invoicesByStatus, InvoiceStatus.Paid),
            Overdue: GetStatusCount(invoicesByStatus, InvoiceStatus.Overdue),
            Cancelled: GetStatusCount(invoicesByStatus, InvoiceStatus.Cancelled),
            Refunded: GetStatusCount(invoicesByStatus, InvoiceStatus.Refunded),
            GrossRevenue: totalIn,
            ClientExpenses: totalOut,
            TotalFees: totalFees,
            NetIncome: totalIn - totalOut - totalFees,
            CreditBalance: client?.CreditBalance ?? 0m,
            ActiveServicesCount: services.Count(s => s.Status == ServiceStatus.Active),
            TotalServicesCount: services.Count,
            TotalDomainsCount: domains.Count,
            AcceptedQuotesCount: quotes.Count(q => q.Status == QuoteStatus.Accepted),
            TotalQuotesCount: quotes.Count,
            OpenTicketsCount: tickets.Count(t => t.Status == TicketStatus.Open),
            TotalTicketsCount: tickets.Count,
            RecentEmails: recentEmails);
    }

    /// <summary>
    /// Retrieves the invoice count and total for a given status from the grouped dictionary.
    /// Returns a zero-valued DTO when the status has no invoices.
    /// </summary>
    /// <param name="grouped">Dictionary of invoice stats grouped by status.</param>
    /// <param name="status">The invoice status to look up.</param>
    /// <returns>Count and total for the requested status.</returns>
    private static InvoiceStatusCountDto GetStatusCount(
        Dictionary<InvoiceStatus, InvoiceStatusCountDto> grouped,
        InvoiceStatus status)
    {
        return grouped.TryGetValue(status, out var dto) ? dto : new InvoiceStatusCountDto(0, 0m);
    }
}
