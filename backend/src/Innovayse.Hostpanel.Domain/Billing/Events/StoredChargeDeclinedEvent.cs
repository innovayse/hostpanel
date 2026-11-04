using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when the gateway refused a charge against a client's stored payment method, so the client
/// can be told to pay the invoice another way.
/// </summary>
/// <param name="ClaimId">The payment claim whose charge was refused.</param>
/// <param name="InvoiceId">The invoice that stays unpaid.</param>
/// <param name="ClientId">The client whose method was charged.</param>
public record StoredChargeDeclinedEvent(int ClaimId, int InvoiceId, int ClientId) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
