using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListGatewayLogs;

/// <summary>
/// Lists the payment-gateway log. No table stores raw gateway calls, so the log is derived from
/// the two records that do exist: recorded invoice transactions and the open hosted-gateway
/// session an invoice still carries.
/// </summary>
public sealed class ListGatewayLogsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListGatewayLogsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles <see cref="ListGatewayLogsQuery"/>.</summary>
    /// <param name="query">The search, the period, the outcome and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with one page of log entries, newest first. Never refuses.</returns>
    public async Task<Result<PaginatedList<GatewayLogEntryDto>>> HandleAsync(
        ListGatewayLogsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var transactions = _context.InvoiceTransactions
            .AsNoTracking()
            .Select(t => new GatewayLogRow
            {
                Id = "tx-" + t.Id.ToString(),
                Date = t.Date,
                Gateway = t.Gateway,
                DebugData = "Invoice #" + t.InvoiceId.ToString() + " ref " + t.TransactionId + " " + (t.Notes ?? string.Empty),
                Result = t.Type == InvoiceTransactionType.Payment ? GatewayLogResult.Payment
                    : t.Type == InvoiceTransactionType.Refund ? GatewayLogResult.Refund
                    : GatewayLogResult.Credit,
                InvoiceId = t.InvoiceId,
            });

        // The session lives on the invoice itself and is cleared once the invoice is paid, so only
        // unsettled attempts appear here; settled ones are in the transaction rows above.
        var sessions = _context.Invoices
            .AsNoTracking()
            .Where(i => i.GatewayModule != null && i.GatewayStartedAt != null)
            .Select(i => new GatewayLogRow
            {
                Id = "session-" + i.Id.ToString(),
                Date = i.GatewayStartedAt!.Value,
                Gateway = i.GatewayModule!,
                DebugData = "Invoice #" + i.Id.ToString() + " order " + (i.GatewayOrderId ?? string.Empty),
                Result = GatewayLogResult.Pending,
                InvoiceId = i.Id,
            });

        var rows = transactions.Concat(sessions);

        rows = rows
            .Search(query.Search, r => r.Gateway, r => r.DebugData)
            .InPeriod(r => r.Date, query.From, query.To);

        if (query.Result.HasValue)
        {
            var result = query.Result.Value;
            rows = rows.Where(r => r.Result == result);
        }

        var ordered = rows.OrderByDescending(r => r.Date).ThenBy(r => r.Id);
        var totalCount = await ordered.CountAsync(ct);
        var page = await ordered.Paginate(query.Page, query.PageSize).ToListAsync(ct);

        var items = page
            .Select(r => new GatewayLogEntryDto(r.Id, r.Date, r.Gateway, r.DebugData.Trim(), r.Result, r.InvoiceId))
            .ToList();

        return Result<PaginatedList<GatewayLogEntryDto>>.Success(
            new PaginatedList<GatewayLogEntryDto>(items, totalCount, query.Page, query.PageSize));
    }
}
