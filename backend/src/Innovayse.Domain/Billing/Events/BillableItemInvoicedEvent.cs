namespace Innovayse.Domain.Billing.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a billable item is marked as invoiced.</summary>
/// <param name="BillableItemId">The billable item that was invoiced.</param>
/// <param name="InvoiceId">The invoice the item was added to.</param>
/// <param name="ClientId">The client the item belongs to.</param>
public record BillableItemInvoicedEvent(int BillableItemId, int InvoiceId, int ClientId) : IDomainEvent;
