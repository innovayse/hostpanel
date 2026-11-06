using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised when staff reject a manual bank transfer because the money did not arrive; the invoice is open again.</summary>
/// <param name="InvoiceId">The invoice that is open again.</param>
/// <param name="ClientId">The client who chose the transfer.</param>
/// <param name="Reference">The reference the payer was told to write on the transfer.</param>
/// <param name="Note">The reason staff gave, for the client's mail.</param>
public sealed record BankTransferRejectedEvent(int InvoiceId, int ClientId, string Reference, string Note) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
