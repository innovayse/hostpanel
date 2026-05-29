namespace Innovayse.Application.Billing.Queries.ListTransactions;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a paginated list of transactions with financial summary totals.</summary>
/// <param name="repo">Transaction repository.</param>
public sealed class ListTransactionsHandler(ITransactionRepository repo)
{
    /// <summary>Handles <see cref="ListTransactionsQuery"/>.</summary>
    /// <param name="query">The query with client ID and pagination params.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the paginated transactions and financial summary.</returns>
    public async Task<TransactionsResultDto> HandleAsync(
        ListTransactionsQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await repo.ListByClientAsync(
            query.ClientId, query.Page, query.PageSize, ct);

        var (totalIn, totalOut, totalFees) = await repo.GetClientSummaryAsync(query.ClientId, ct);

        var dtos = items.Select(t => new TransactionDto(
            t.Id, t.ClientId, t.Date, t.Description, t.TransactionId,
            t.InvoiceId, t.PaymentMethod, t.AmountIn, t.AmountOut, t.Fees, t.AddedToCredit))
            .ToList();

        var paged = new PagedResult<TransactionDto>(dtos, totalCount, query.Page, query.PageSize);
        var balance = totalIn - totalOut - totalFees;

        return new TransactionsResultDto(paged, totalIn, totalOut, totalFees, balance);
    }
}
