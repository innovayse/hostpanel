namespace Innovayse.Application.Billing.Commands.AddInvoicePayment;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Records a manual payment transaction against an invoice
/// and creates a corresponding client-level transaction ledger entry.
/// </summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="transactionRepo">Client transaction repository for the ledger entry.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class AddInvoicePaymentHandler(
    IInvoiceRepository repo,
    ITransactionRepository transactionRepo,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
{
    /// <summary>
    /// Handles <see cref="AddInvoicePaymentCommand"/>.
    /// </summary>
    /// <param name="cmd">The add payment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the payment is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(AddInvoicePaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.AddPayment(cmd.Date, cmd.Gateway, cmd.TransactionId, cmd.Amount, cmd.Fees, cmd.Notes);

        var clientTx = Transaction.Create(
            invoice.ClientId,
            cmd.Date,
            $"Invoice #{cmd.InvoiceId} Payment",
            cmd.TransactionId,
            cmd.InvoiceId,
            cmd.Gateway,
            cmd.Amount,
            0m,
            cmd.Fees,
            addedToCredit: false);
        transactionRepo.Add(clientTx);

        activityLogRepo.Add(ActivityLog.Create(
            invoice.ClientId,
            $"Added Invoice Payment - Invoice ID: {invoice.Id}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));

        await uow.SaveChangesAsync(ct);
    }
}
