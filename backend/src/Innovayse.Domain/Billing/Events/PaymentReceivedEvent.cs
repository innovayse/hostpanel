namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a payment is successfully recorded on an invoice.</summary>
/// <param name="InvoiceId">The paid invoice ID.</param>
/// <param name="ClientId">The client who paid.</param>
/// <param name="Amount">The amount paid.</param>
/// <param name="TransactionId">The gateway transaction reference.</param>
public record PaymentReceivedEvent(int InvoiceId, int ClientId, decimal Amount, string TransactionId) : IDomainEvent;
