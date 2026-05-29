namespace Innovayse.Application.Billing.Commands.AddInvoicePayment;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Records a manual payment transaction against an invoice
/// and creates a corresponding client-level transaction ledger entry.
/// </summary>
public sealed class AddInvoicePaymentHandler(
    IInvoiceRepository repo,
<<<<<<< HEAD
    ITransactionRepository transactionRepo,
=======
    IClientTransactionRepository transactionRepo,
>>>>>>> origin/main
    IUnitOfWork uow)
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

<<<<<<< HEAD
        var clientTx = Transaction.Create(
=======
        var clientTx = ClientTransaction.Create(
>>>>>>> origin/main
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

        await uow.SaveChangesAsync(ct);
    }
}
