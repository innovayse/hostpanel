namespace Innovayse.Application.Billing.Queries.GetTransaction;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Handles <see cref="GetTransactionByIdQuery"/>.</summary>
/// <param name="repo">Transaction repository.</param>
/// <param name="clientRepo">Client repository for resolving the client name.</param>
public sealed class GetTransactionByIdHandler(ITransactionRepository repo, IClientRepository clientRepo)
{
    /// <summary>Returns the transaction with the given ID.</summary>
    /// <param name="query">The query carrying the transaction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transaction DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDto> HandleAsync(GetTransactionByIdQuery query, CancellationToken ct)
    {
        var t = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {query.Id} not found.");

        var clients = await clientRepo.FindByIdsAsync([t.ClientId], ct);
        var client = clients.FirstOrDefault();
        var clientName = client is not null
            ? $"{client.FirstName} {client.LastName}".Trim()
            : $"Client #{t.ClientId}";

        return new TransactionDto(
            t.Id, t.ClientId, clientName, t.Date, t.Description, t.TransactionId,
            t.InvoiceId, t.PaymentMethod, t.AmountIn, t.AmountOut, t.Fees, t.AddedToCredit);
    }
}
