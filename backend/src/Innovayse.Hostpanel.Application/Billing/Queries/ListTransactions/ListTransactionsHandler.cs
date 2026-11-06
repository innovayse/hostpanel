using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListTransactions;

/// <summary>Returns a paginated list of transactions with financial summary totals.</summary>
public sealed class ListTransactionsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    public ListTransactionsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ListTransactionsQuery"/>.</summary>
    /// <param name="query">The query with client ID and pagination params.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the paginated transactions and financial summary. Never refuses.</returns>
    public async Task<Result<TransactionsResultDto>> HandleAsync(
        ListTransactionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Credit-only lines moved no cash and belong to the credit history, not to this list.
        IQueryable<Transaction> filtered = _context.Transactions.AsNoTracking().Where(Transaction.MovedMoneyFilter);
        if (query.ClientId.HasValue)
        {
            filtered = filtered.Where(t => t.ClientId == query.ClientId.Value);
        }

        var ordered = filtered.OrderByDescending(t => t.Date);
        var totalCount = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // Rows are in their own currencies, so each currency is summed on its own and only then converted
        // to the base currency; adding drams to dollars as if they were one unit misstates every tile.
        var perCurrency = await filtered
            .GroupBy(t => t.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                TotalIn = g.Sum(t => t.AmountIn),
                TotalOut = g.Sum(t => t.AmountOut),
                TotalFees = g.Sum(t => t.Fees),
            })
            .ToListAsync(ct);
        var rates = await _context.GetRatesToBaseAsync(ct);
        var totalIn = perCurrency.Sum(c => rates.ToBase(c.TotalIn, c.Currency));
        var totalOut = perCurrency.Sum(c => rates.ToBase(c.TotalOut, c.Currency));
        var totalFees = perCurrency.Sum(c => rates.ToBase(c.TotalFees, c.Currency));
        var baseCurrency = await _context.GetBaseCurrencyAsync(ct);

        var clientIds = items.Select(t => t.ClientId).Distinct().ToList();
        var clientMap = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items.Select(t => new TransactionDto(
            t.Id, t.ClientId,
            clientMap[t.ClientId].FullName,
            t.Date, t.Description, t.TransactionId,
            t.InvoiceId, t.PaymentMethod, t.AmountIn, t.AmountOut, t.Fees, t.AddedToCredit, t.Currency))
            .ToList();

        var paged = new PaginatedList<TransactionDto>(dtos, totalCount, query.Page, query.PageSize);
        var balance = totalIn - totalOut - totalFees;

        return Result<TransactionsResultDto>.Success(
            new TransactionsResultDto(paged, totalIn, totalOut, totalFees, balance, baseCurrency.Code));
    }
}
