using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// An amount to move onto or off a client's credit balance, and why. Every movement is written to the client ledger with
/// the balance it left, so a caller always says which kind of movement it is.
/// </summary>
/// <param name="ClientId">The client whose balance changes.</param>
/// <param name="Amount">The positive amount, in the currency the balance is kept in.</param>
/// <param name="Movement">Why the balance moves; names the credit history line.</param>
/// <param name="InvoiceId">The invoice the credit moves to or from, or <see langword="null"/> when it belongs to none.</param>
/// <param name="Note">A reason to keep with the history line (staff adjustments), or <see langword="null"/> for the standard wording.</param>
/// <param name="Entry">
/// The ledger entry that already records the money behind this movement (a payment, a refund). It is stamped with the
/// movement instead of a second line being written; <see langword="null"/> when the movement moved no cash, and a
/// credit-only entry is written for it.
/// </param>
public sealed record ClientCreditChange(
    int ClientId,
    decimal Amount,
    CreditMovementType Movement,
    int? InvoiceId = null,
    string? Note = null,
    Transaction? Entry = null);
