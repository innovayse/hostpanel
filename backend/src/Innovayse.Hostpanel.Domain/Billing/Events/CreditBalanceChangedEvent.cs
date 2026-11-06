using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when a movement of a client's credit balance is recorded on the ledger and the client is to be told the
/// new balance (<see cref="CreditMovementTypeExtensions.NotifiesClient"/>).
/// </summary>
/// <param name="ClientId">The client whose balance moved.</param>
/// <param name="Movement">Why it moved.</param>
/// <param name="Change">The signed change: positive when credit was added, negative when it was taken.</param>
/// <param name="BalanceAfter">The balance once the change was saved.</param>
/// <param name="Currency">ISO 4217 code the balance is held in.</param>
public sealed record CreditBalanceChangedEvent(
    int ClientId, CreditMovementType Movement, decimal Change, decimal BalanceAfter, string Currency) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
