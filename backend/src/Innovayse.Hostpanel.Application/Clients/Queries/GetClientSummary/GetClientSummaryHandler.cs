using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
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
        var invoiceRows = await _context.Invoices.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .GroupBy(i => new { i.Status, i.Currency })
            .Select(g => new { g.Key.Status, g.Key.Currency, Count = g.Count(), Total = g.Sum(i => i.Total) })
            .ToListAsync(ct);
        var invoicesByStatus = invoiceRows
            .GroupBy(r => r.Status)
            .ToDictionary(
                g => g.Key,
                g => new InvoiceStatusCountDto(
                    g.Sum(r => r.Count),
                    g.OrderBy(r => r.Currency, StringComparer.Ordinal).Select(r => new CurrencyAmountDto(r.Total, r.Currency)).ToList()));

        var transactionRows = await _context.Transactions.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .GroupBy(t => t.Currency)
            .Select(g => new { Currency = g.Key, TotalIn = g.Sum(t => t.AmountIn), TotalOut = g.Sum(t => t.AmountOut), TotalFees = g.Sum(t => t.Fees) })
            .ToListAsync(ct);
        transactionRows.Sort((x, y) => string.CompareOrdinal(x.Currency, y.Currency));

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

        // The credit is held in the currency the rest of billing reads it in: the shared rule, not a second one.
        var creditCurrency = await _context.GetCreditCurrencyAsync(client, ct);

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
                GrossRevenue: transactionRows.Select(r => new CurrencyAmountDto(r.TotalIn, r.Currency)).ToList(),
                ClientExpenses: transactionRows.Select(r => new CurrencyAmountDto(r.TotalOut, r.Currency)).ToList(),
                TotalFees: transactionRows.Select(r => new CurrencyAmountDto(r.TotalFees, r.Currency)).ToList(),
                NetIncome: transactionRows.Select(r => new CurrencyAmountDto(r.TotalIn - r.TotalOut - r.TotalFees, r.Currency)).ToList()),
            CreditBalance: new CurrencyAmountDto(client?.CreditBalance ?? 0m, creditCurrency),
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
    /// Returns a zero-count DTO with no totals when the status has no invoices.
    /// </summary>
    /// <param name="grouped">Dictionary of invoice stats grouped by status.</param>
    /// <param name="status">The invoice status to look up.</param>
    /// <returns>Count and total for the requested status.</returns>
    private static InvoiceStatusCountDto GetStatusCount(
        Dictionary<InvoiceStatus, InvoiceStatusCountDto> grouped, InvoiceStatus status) =>
        grouped.TryGetValue(status, out var dto) ? dto : new InvoiceStatusCountDto(0, []);
}
