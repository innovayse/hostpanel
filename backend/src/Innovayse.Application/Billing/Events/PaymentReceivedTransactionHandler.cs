namespace Innovayse.Application.Billing.Events;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
<<<<<<< HEAD
/// Handles <see cref="PaymentReceivedEvent"/> by creating a transaction record
/// for the payment received on an invoice.
/// </summary>
/// <param name="transactionRepo">Transaction repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class PaymentReceivedTransactionHandler(
    ITransactionRepository transactionRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates a transaction recording the payment.
=======
/// Handles <see cref="PaymentReceivedEvent"/> by creating a client transaction record
/// for the payment received on an invoice.
/// </summary>
/// <param name="transactionRepo">Client transaction repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class PaymentReceivedTransactionHandler(
    IClientTransactionRepository transactionRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates a client transaction recording the payment.
>>>>>>> origin/main
    /// </summary>
    /// <param name="evt">The domain event carrying payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
<<<<<<< HEAD
        var transaction = Transaction.Create(
=======
        var transaction = ClientTransaction.Create(
>>>>>>> origin/main
            evt.ClientId,
            DateTimeOffset.UtcNow,
            $"Invoice #{evt.InvoiceId} Payment",
            evt.TransactionId,
            evt.InvoiceId,
            "Payment Gateway",
            evt.Amount,
            0m,
            0m,
            false);

        transactionRepo.Add(transaction);
        await uow.SaveChangesAsync(ct);
    }
}
