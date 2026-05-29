namespace Innovayse.Application.Billing.Commands.CreateTransaction;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Creates a new transaction record.</summary>
public sealed class CreateTransactionHandler(ITransactionRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateTransactionCommand"/>.
    /// </summary>
    /// <param name="cmd">The create transaction command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created transaction ID.</returns>
    public async Task<int> HandleAsync(CreateTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = Transaction.Create(
            cmd.ClientId,
            invoiceId: null,
            cmd.Type,
            cmd.Amount,
            cmd.Fees,
            cmd.Currency,
            cmd.Description,
            cmd.Gateway,
            cmd.TransactionId);

        repo.Add(transaction);
        await uow.SaveChangesAsync(ct);
        return transaction.Id;
    }
}
