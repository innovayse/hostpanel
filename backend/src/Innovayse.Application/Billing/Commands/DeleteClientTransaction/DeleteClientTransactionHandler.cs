namespace Innovayse.Application.Billing.Commands.DeleteClientTransaction;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Deletes a <see cref="Domain.Billing.ClientTransaction"/> and reverses any credit adjustments
/// that were applied when the transaction was created.
/// </summary>
/// <param name="transactionRepo">Client transaction repository.</param>
/// <param name="clientRepo">Client repository for credit reversals.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class DeleteClientTransactionHandler(
    IClientTransactionRepository transactionRepo,
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteClientTransactionCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the transaction or client is not found.</exception>
    public async Task HandleAsync(DeleteClientTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await transactionRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Client transaction {cmd.Id} not found.");

        if (transaction.AddedToCredit)
        {
            var client = await clientRepo.FindByIdAsync(transaction.ClientId, ct)
                ?? throw new InvalidOperationException($"Client {transaction.ClientId} not found.");

            if (transaction.AmountIn > 0)
            {
                client.DeductCredit(transaction.AmountIn);
            }

            if (transaction.AmountOut > 0)
            {
                client.AddCredit(transaction.AmountOut);
            }
        }

        transactionRepo.Remove(transaction);
        await uow.SaveChangesAsync(ct);
    }
}
