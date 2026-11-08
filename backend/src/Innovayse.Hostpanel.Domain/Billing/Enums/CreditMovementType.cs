namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// Says why a client's credit balance moved. Every movement of the balance is recorded on the client
/// ledger (<see cref="Transaction"/>) under one of these, with the signed change and the balance it left.
/// The names are persisted, so they are never renamed.
/// </summary>
public enum CreditMovementType
{
    /// <summary>Staff added credit to the balance.</summary>
    AdminAdd = 0,

    /// <summary>Staff took credit off the balance.</summary>
    AdminDeduct = 1,

    /// <summary>Credit was taken off the balance and applied to an invoice, by staff or by the client.</summary>
    InvoiceApplied = 2,

    /// <summary>Credit an invoice held went back to the balance (the credit was removed, or the invoice was cancelled or lowered).</summary>
    InvoiceReturned = 3,

    /// <summary>The renewal sweep applied credit to a renewal invoice on its own.</summary>
    RenewalAutoApplied = 4,

    /// <summary>A refund was paid back as credit instead of to the payer.</summary>
    RefundToCredit = 5,

    /// <summary>A paid credit top-up invoice put its amount on the balance.</summary>
    TopUp = 6,

    /// <summary>A refund of a paid top-up took the refunded amount off the balance again.</summary>
    TopUpRefunded = 7,

    /// <summary>A payment took more than the invoice owed, or could not be recorded on its invoice, and the money went to the balance.</summary>
    PaymentSurplus = 8,

    /// <summary>Staff deleted a legacy ledger entry that had moved the balance, and the move was undone.</summary>
    TransactionReversed = 9,

    /// <summary>A credit note issued as account credit put its value on the balance.</summary>
    CreditNoteIssued = 10,

    /// <summary>A credit note issued as account credit was voided, and its value came off the balance again.</summary>
    CreditNoteVoided = 11,

    /// <summary>A credit note issued as account credit put its value on the balance.</summary>
    CreditNoteIssued = 10,

    /// <summary>A credit note issued as account credit was voided, and its value came off the balance again.</summary>
    CreditNoteVoided = 11,
}
