namespace Innovayse.Application.Billing.Queries.GetTransaction;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a single transaction by ID with client name.</summary>
public sealed class GetTransactionByIdHandler(ITransactionRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="GetTransactionByIdQuery"/>.
    /// </summary>
    /// <param name="query">The get transaction by ID query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transaction DTO.</returns>
    public async Task<TransactionDto> HandleAsync(GetTransactionByIdQuery query, CancellationToken ct)
    {
        var transaction = await repo.FindByIdAsync(query.Id, ct);
        if (transaction is null)
            throw new InvalidOperationException($"Transaction {query.Id} not found.");

        var client = await clientRepo.FindByIdAsync(transaction.ClientId, ct);
        var clientName = client != null ? $"{client.FirstName} {client.LastName}" : "Unknown";

        return new TransactionDto(
            transaction.Id,
            transaction.ClientId,
            clientName,
            transaction.InvoiceId,
            transaction.Type,
            transaction.Amount,
            transaction.Fees,
            transaction.Currency,
            transaction.Gateway,
            transaction.TransactionId,
            transaction.Description,
            transaction.CreatedAt);
    }
}
