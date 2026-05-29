namespace Innovayse.Application.Billing.Queries.ListClientTransactions;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Returns a paginated list of client transactions with financial summary totals.
/// </summary>
/// <param name="repo">Client transaction repository.</param>
public sealed class ListClientTransactionsHandler(IClientTransactionRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListClientTransactionsQuery"/>.
    /// </summary>
    /// <param name="query">The query with client ID and pagination params.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the paginated transactions and financial summary.</returns>
    public async Task<ClientTransactionsResultDto> HandleAsync(
        ListClientTransactionsQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await repo.ListByClientAsync(
            query.ClientId, query.Page, query.PageSize, ct);

        var (totalIn, totalOut, totalFees) = await repo.GetClientSummaryAsync(query.ClientId, ct);

        var dtos = items.Select(t => new ClientTransactionDto(
            t.Id,
            t.ClientId,
            t.Date,
            t.Description,
            t.TransactionId,
            t.InvoiceId,
            t.PaymentMethod,
            t.AmountIn,
            t.AmountOut,
            t.Fees,
            t.AddedToCredit)).ToList();

        var paged = new PagedResult<ClientTransactionDto>(dtos, totalCount, query.Page, query.PageSize);
        var balance = totalIn - totalOut - totalFees;

        return new ClientTransactionsResultDto(paged, totalIn, totalOut, totalFees, balance);
    }
}
