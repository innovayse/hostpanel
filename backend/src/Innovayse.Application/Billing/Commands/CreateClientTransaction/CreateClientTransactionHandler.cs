namespace Innovayse.Application.Billing.Commands.CreateClientTransaction;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Creates a <see cref="ClientTransaction"/> and optionally adjusts the client's credit balance.
/// </summary>
/// <param name="transactionRepo">Client transaction repository.</param>
/// <param name="clientRepo">Client repository for credit adjustments.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class CreateClientTransactionHandler(
    IClientTransactionRepository transactionRepo,
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateClientTransactionCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created transaction's ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task<int> HandleAsync(CreateClientTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = ClientTransaction.Create(
            cmd.ClientId,
            cmd.Date,
            cmd.Description,
            cmd.TransactionId,
            cmd.InvoiceId,
            cmd.PaymentMethod,
            cmd.AmountIn,
            cmd.AmountOut,
            cmd.Fees,
            cmd.AddToCredit);

        transactionRepo.Add(transaction);

        if (cmd.AddToCredit)
        {
            var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
                ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

            if (cmd.AmountIn > 0)
            {
                client.AddCredit(cmd.AmountIn);
            }

            if (cmd.AmountOut > 0)
            {
                client.DeductCredit(cmd.AmountOut);
            }
        }

        await uow.SaveChangesAsync(ct);
        return transaction.Id;
    }
}
