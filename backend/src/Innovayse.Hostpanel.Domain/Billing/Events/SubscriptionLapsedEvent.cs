using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised once when an overdue invoice is marked lapsed: the period it billed was never paid for.</summary>
/// <param name="InvoiceId">The lapsed invoice's id.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="HasDigitalItems">Whether the invoice charges for at least one digital product, so fulfilment plugins have something to end.</param>
public sealed record SubscriptionLapsedEvent(int InvoiceId, int ClientId, bool HasDigitalItems) : IDomainEvent
{
    /// <summary>When the invoice lapsed, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
