namespace Innovayse.Application.Billing.Queries.GetTransaction;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="GetTransactionByIdQuery"/>.</summary>
/// <param name="repo">Transaction repository.</param>
public sealed class GetTransactionByIdHandler(ITransactionRepository repo)
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

        return new TransactionDto(
            t.Id, t.ClientId, t.Date, t.Description, t.TransactionId,
            t.InvoiceId, t.PaymentMethod, t.AmountIn, t.AmountOut, t.Fees, t.AddedToCredit);
    }
}
