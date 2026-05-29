namespace Innovayse.Application.Billing.Queries.ListTransactions;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a paginated list of all transactions for admin consumption with client names.</summary>
public sealed class ListTransactionsHandler(ITransactionRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="ListTransactionsQuery"/>.
    /// </summary>
    /// <param name="query">The list transactions query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of transaction list items.</returns>
    public async Task<PagedResult<TransactionDto>> HandleAsync(ListTransactionsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListAsync(page, pageSize, ct);

        // Batch-resolve client names
        var clientIds = items.Select(tx => tx.ClientId).Distinct();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        var dtos = items.Select(tx => new TransactionDto(
            tx.Id,
            tx.ClientId,
            clientMap.GetValueOrDefault(tx.ClientId, "Unknown"),
            tx.InvoiceId,
            tx.Type,
            tx.Amount,
            tx.Fees,
            tx.Currency,
            tx.Gateway,
            tx.TransactionId,
            tx.Description,
            tx.CreatedAt))
            .ToList();

        return new PagedResult<TransactionDto>(dtos, total, page, pageSize);
    }
}
