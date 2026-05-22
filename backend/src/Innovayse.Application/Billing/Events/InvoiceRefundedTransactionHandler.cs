namespace Innovayse.Application.Billing.Events;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="InvoiceRefundedEvent"/> by creating a client transaction record
/// for the refund and adding the refunded amount to the client's credit balance.
/// </summary>
/// <param name="transactionRepo">Client transaction repository.</param>
/// <param name="clientRepo">Client repository for credit adjustments.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class InvoiceRefundedTransactionHandler(
    IClientTransactionRepository transactionRepo,
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates a client transaction recording the refund and adds credit to the client.
    /// </summary>
    /// <param name="evt">The domain event carrying refund details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task HandleAsync(InvoiceRefundedEvent evt, CancellationToken ct)
    {
        var transaction = ClientTransaction.Create(
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
