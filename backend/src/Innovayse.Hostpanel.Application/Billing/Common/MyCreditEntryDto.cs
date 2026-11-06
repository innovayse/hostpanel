using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One line of the client's own credit history. It carries no description: the ledger description of a staff
/// adjustment is the staff's note, which is not written for the client, so the client reads the movement and the invoice only.
/// </summary>
/// <param name="Id">The ledger entry that records the movement.</param>
/// <param name="Date">When the balance moved (UTC).</param>
/// <param name="Movement">Why it moved.</param>
/// <param name="Change">The signed change: positive when credit was added, negative when it was taken.</param>
/// <param name="BalanceAfter">The balance once the change was saved.</param>
/// <param name="InvoiceId">The invoice the credit moved to or from, or <see langword="null"/>.</param>
public sealed record MyCreditEntryDto(
    int Id,
    DateTimeOffset Date,
    CreditMovementType Movement,
    decimal Change,
    decimal BalanceAfter,
    int? InvoiceId);
