namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a draft invoice is published and becomes payable.</summary>
/// <param name="InvoiceId">The published invoice ID.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
public record InvoicePublishedEvent(int InvoiceId, int ClientId) : IDomainEvent;
