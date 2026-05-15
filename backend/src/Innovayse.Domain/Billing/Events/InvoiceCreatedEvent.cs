namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new invoice is created.</summary>
/// <param name="InvoiceId">The invoice ID (0 before save; EF sets the real value post-persist).</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
public record InvoiceCreatedEvent(int InvoiceId, int ClientId) : IDomainEvent;
