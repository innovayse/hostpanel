namespace Innovayse.Application.Billing.Queries.ListTransactions;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a paginated list of transactions with financial summary totals.</summary>
/// <param name="repo">Transaction repository.</param>
/// <param name="clientRepo">Client repository for resolving names.</param>
public sealed class ListTransactionsHandler(ITransactionRepository repo, IClientRepository clientRepo)
{
    /// <summary>Handles <see cref="ListTransactionsQuery"/>.</summary>
    /// <param name="query">The query with client ID and pagination params.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the paginated transactions and financial summary.</returns>
    public async Task<TransactionsResultDto> HandleAsync(
        ListTransactionsQuery query, CancellationToken ct)
    {
        var (items, totalCount) = query.ClientId.HasValue
            ? await repo.ListByClientAsync(query.ClientId.Value, query.Page, query.PageSize, ct)
            : await repo.ListAllAsync(query.Page, query.PageSize, ct);

        var (totalIn, totalOut, totalFees) = query.ClientId.HasValue
            ? await repo.GetClientSummaryAsync(query.ClientId.Value, ct)
            : (0m, 0m, 0m);

        var clientIds = items.Select(t => t.ClientId).Distinct().ToList();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());

        var dtos = items.Select(t => new TransactionDto(
            t.Id, t.ClientId,
            clientMap.TryGetValue(t.ClientId, out var name) ? name : $"Client #{t.ClientId}",
            t.Date, t.Description, t.TransactionId,
            t.InvoiceId, t.PaymentMethod, t.AmountIn, t.AmountOut, t.Fees, t.AddedToCredit))
            .ToList();

        var paged = new PagedResult<TransactionDto>(dtos, totalCount, query.Page, query.PageSize);
        var balance = totalIn - totalOut - totalFees;

        return new TransactionsResultDto(paged, totalIn, totalOut, totalFees, balance);
    }
}
