using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when a charge against a client's stored payment method needs the payer to authenticate before it
/// completes, so the client can be sent the link to do it.
/// </summary>
/// <param name="ClaimId">The payment claim waiting for the payer.</param>
/// <param name="InvoiceId">The invoice being paid.</param>
/// <param name="ClientId">The client whose method was charged.</param>
/// <param name="ActionUrl">Where the payer authenticates, or <see langword="null"/> when the plugin gave none.</param>
public record StoredChargeActionRequiredEvent(int ClaimId, int InvoiceId, int ClientId, string? ActionUrl) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
