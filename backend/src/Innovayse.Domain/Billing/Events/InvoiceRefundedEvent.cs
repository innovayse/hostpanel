namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a paid invoice is refunded.</summary>
/// <param name="InvoiceId">The refunded invoice ID.</param>
/// <param name="ClientId">The client who received the refund.</param>
/// <param name="Amount">The refunded amount.</param>
public record InvoiceRefundedEvent(int InvoiceId, int ClientId, decimal Amount) : IDomainEvent;
