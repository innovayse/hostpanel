namespace Innovayse.Application.Billing.Events;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
<<<<<<< HEAD
/// Handles <see cref="InvoiceRefundedEvent"/> by creating a transaction record
/// for the refund and adding the refunded amount to the client's credit balance.
/// </summary>
/// <param name="transactionRepo">Transaction repository.</param>
/// <param name="clientRepo">Client repository for credit adjustments.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class InvoiceRefundedTransactionHandler(
    ITransactionRepository transactionRepo,
=======
/// Handles <see cref="InvoiceRefundedEvent"/> by creating a client transaction record
/// for the refund and adding the refunded amount to the client's credit balance.
/// </summary>
/// <param name="transactionRepo">Client transaction repository.</param>
/// <param name="clientRepo">Client repository for credit adjustments.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class InvoiceRefundedTransactionHandler(
    IClientTransactionRepository transactionRepo,
>>>>>>> origin/main
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
<<<<<<< HEAD
    /// Creates a transaction recording the refund and adds credit to the client.
=======
    /// Creates a client transaction recording the refund and adds credit to the client.
>>>>>>> origin/main
    /// </summary>
    /// <param name="evt">The domain event carrying refund details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task HandleAsync(InvoiceRefundedEvent evt, CancellationToken ct)
    {
<<<<<<< HEAD
        var transaction = Transaction.Create(
=======
        var transaction = ClientTransaction.Create(
>>>>>>> origin/main
            evt.ClientId,
            DateTimeOffset.UtcNow,
            $"Invoice #{evt.InvoiceId} Refund",
            string.Empty,
            evt.InvoiceId,
            "Refund",
            0m,
            evt.Amount,
            0m,
            true);

        transactionRepo.Add(transaction);

        var client = await clientRepo.FindByIdAsync(evt.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {evt.ClientId} not found.");

        client.AddCredit(evt.Amount);

        await uow.SaveChangesAsync(ct);
    }
}
