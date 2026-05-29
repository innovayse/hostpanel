namespace Innovayse.Application.Billing.Events;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
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
    /// </summary>
    /// <param name="evt">The domain event carrying payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        var transaction = Transaction.Create(
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
