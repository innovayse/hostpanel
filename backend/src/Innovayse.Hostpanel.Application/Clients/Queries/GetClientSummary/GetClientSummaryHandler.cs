using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClientSummary;

/// <summary>
/// Aggregates data across the client's invoices, transactions, services, domains, quotes,
/// tickets and mail into one dashboard summary.
/// </summary>
public sealed class GetClientSummaryHandler
{
    /// <summary>How many recent email log entries the summary carries.</summary>
    private const int RecentEmailsLimit = 5;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the client's sign-in address, for the recent-mail section.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the client's sign-in address, for the recent-mail section.</param>
    public GetClientSummaryHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Executes the query by aggregating every section sequentially.</summary>
    /// <param name="query">The query containing the client id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The aggregated summary. A client id that names nobody answers a zero-valued summary rather
    /// than a refusal, because the dashboard this backs does not distinguish the two.
    /// </returns>
    public async Task<Result<ClientSummaryDto>> HandleAsync(GetClientSummaryQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientId = query.ClientId;

        // EF Core's DbContext is not thread-safe: the sections are read one after another.
        var invoicesByStatus = await _context.Invoices.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(i => i.Total) })
            .ToDictionaryAsync(g => g.Status, g => new InvoiceStatusCountDto(g.Count, g.Total), ct);

        var transactionTotals = await _context.Transactions.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .GroupBy(_ => 1)
            .Select(g => new { TotalIn = g.Sum(t => t.AmountIn), TotalOut = g.Sum(t => t.AmountOut), TotalFees = g.Sum(t => t.Fees) })
            .FirstOrDefaultAsync(ct);

        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);

        var activeServicesCount = await _context.ClientServices.AsNoTracking()
            .CountAsync(s => s.ClientId == clientId && s.Status == ServiceStatus.Active, ct);
        var totalServicesCount = await _context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, ct);

        var totalDomainsCount = await _context.Domains.AsNoTracking().CountAsync(d => d.ClientId == clientId, ct);

        var acceptedQuotesCount = await _context.Quotes.AsNoTracking()
            .CountAsync(q => q.ClientId == clientId && q.Stage == QuoteStage.Accepted, ct);
        var totalQuotesCount = await _context.Quotes.AsNoTracking().CountAsync(q => q.ClientId == clientId, ct);

        var openTicketsCount = await _context.Tickets.AsNoTracking()
            .CountAsync(t => t.ClientId == clientId && t.Status == TicketStatus.Open, ct);
        var totalTicketsCount = await _context.Tickets.AsNoTracking().CountAsync(t => t.ClientId == clientId, ct);

        var recentEmails = new List<EmailLogDto>();
        var account = client is null ? null : await _identity.FindBySubjectAsync(client.UserId, ct);
        if (account is not null)
        {
            var logs = await _context.EmailLogs.AsNoTracking()
                .Where(e => e.To == account.Email)
                .OrderByDescending(e => e.SentAt)
                .Take(RecentEmailsLimit)
                .SelectDto()
                .ToListAsync(ct);
            recentEmails.AddRange(logs);
        }

        var totalIn = transactionTotals?.TotalIn ?? 0m;
        var totalOut = transactionTotals?.TotalOut ?? 0m;
        var totalFees = transactionTotals?.TotalFees ?? 0m;

        var summary = new ClientSummaryDto(
            Invoices: new ClientInvoiceStatusesDto(
                Draft: GetStatusCount(invoicesByStatus, InvoiceStatus.Draft),
                Unpaid: GetStatusCount(invoicesByStatus, InvoiceStatus.Unpaid),
                Paid: GetStatusCount(invoicesByStatus, InvoiceStatus.Paid),
                Overdue: GetStatusCount(invoicesByStatus, InvoiceStatus.Overdue),
                Cancelled: GetStatusCount(invoicesByStatus, InvoiceStatus.Cancelled),
                Refunded: GetStatusCount(invoicesByStatus, InvoiceStatus.Refunded),
                PartiallyRefunded: GetStatusCount(invoicesByStatus, InvoiceStatus.PartiallyRefunded)),
            Transactions: new ClientTransactionTotalsDto(
                GrossRevenue: totalIn,
                ClientExpenses: totalOut,
                TotalFees: totalFees,
                NetIncome: totalIn - totalOut - totalFees),
            CreditBalance: client?.CreditBalance ?? 0m,
            Counts: new ClientRecordCountsDto(
                ActiveServicesCount: activeServicesCount,
                TotalServicesCount: totalServicesCount,
                TotalDomainsCount: totalDomainsCount,
                AcceptedQuotesCount: acceptedQuotesCount,
                TotalQuotesCount: totalQuotesCount,
                OpenTicketsCount: openTicketsCount,
                TotalTicketsCount: totalTicketsCount),
            RecentEmails: recentEmails);

        return Result<ClientSummaryDto>.Success(summary);
    }

    /// <summary>
    /// Retrieves the invoice count and total for a given status from the grouped dictionary.
    /// Returns a zero-valued DTO when the status has no invoices.
    /// </summary>
    /// <param name="grouped">Dictionary of invoice stats grouped by status.</param>
    /// <param name="status">The invoice status to look up.</param>
    /// <returns>Count and total for the requested status.</returns>
    private static InvoiceStatusCountDto GetStatusCount(
        Dictionary<InvoiceStatus, InvoiceStatusCountDto> grouped, InvoiceStatus status) =>
        grouped.TryGetValue(status, out var dto) ? dto : new InvoiceStatusCountDto(0, 0m);
}
