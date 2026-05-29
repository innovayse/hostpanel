namespace Innovayse.Application.Billing.Commands.UpdateTransaction;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Updates an existing transaction record.</summary>
public sealed class UpdateTransactionHandler(ITransactionRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateTransactionCommand"/>.
    /// </summary>
    /// <param name="cmd">The update transaction command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(UpdateTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await repo.FindByIdAsync(cmd.Id, ct);
        if (transaction is null)
            throw new InvalidOperationException($"Transaction {cmd.Id} not found.");

        transaction.Update(
            cmd.ClientId,
            cmd.Type,
            cmd.Amount,
            cmd.Fees,
            cmd.Currency,
            cmd.Description,
            cmd.Gateway,
            cmd.TransactionId);

        await uow.SaveChangesAsync(ct);
    }
}
