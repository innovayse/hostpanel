using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One line of a client's credit history as staff read it: a movement of the balance and what it left.</summary>
/// <param name="Id">The ledger entry that records the movement.</param>
/// <param name="Date">When the balance moved (UTC).</param>
/// <param name="Movement">Why it moved.</param>
/// <param name="Change">The signed change: positive when credit was added, negative when it was taken.</param>
/// <param name="BalanceAfter">The balance once the change was saved.</param>
/// <param name="InvoiceId">The invoice the credit moved to or from, or <see langword="null"/>.</param>
/// <param name="Description">The ledger description, which carries the reason staff gave for an adjustment.</param>
public sealed record CreditEntryDto(
    int Id,
    DateTimeOffset Date,
    CreditMovementType Movement,
    decimal Change,
    decimal BalanceAfter,
    int? InvoiceId,
    string Description);
