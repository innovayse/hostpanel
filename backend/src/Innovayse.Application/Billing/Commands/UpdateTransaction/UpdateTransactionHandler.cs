namespace Innovayse.Application.Billing.Commands.UpdateTransaction;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Updates an existing <see cref="Domain.Billing.Transaction"/>.</summary>
/// <param name="transactionRepo">Transaction repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class UpdateTransactionHandler(
    ITransactionRepository transactionRepo,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
{
    /// <summary>Handles <see cref="UpdateTransactionCommand"/>.</summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the transaction is not found.</exception>
    public async Task HandleAsync(UpdateTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await transactionRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {cmd.Id} not found.");

        transaction.Update(
            cmd.Date,
            cmd.Description,
            cmd.TransactionId,
            cmd.InvoiceId,
            cmd.PaymentMethod,
            cmd.AmountIn,
            cmd.AmountOut,
            cmd.Fees);

        await uow.SaveChangesAsync(ct);

        activityLogRepo.Add(ActivityLog.Create(
            transaction.ClientId,
            $"Updated Transaction - Transaction ID: {transaction.Id}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));
        await uow.SaveChangesAsync(ct);
    }
}
