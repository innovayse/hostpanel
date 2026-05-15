namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when an invoice is marked overdue.</summary>
/// <param name="InvoiceId">The overdue invoice ID.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
public record InvoiceOverdueEvent(int InvoiceId, int ClientId) : IDomainEvent;
