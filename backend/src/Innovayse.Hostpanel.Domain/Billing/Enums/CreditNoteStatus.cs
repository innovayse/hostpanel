namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// Lifecycle states of a <see cref="CreditNote"/>. The names are persisted, so they are never renamed.
/// </summary>
public enum CreditNoteStatus
{
    /// <summary>The note is being prepared: it has no number yet and nothing has moved.</summary>
    Draft = 0,

    /// <summary>The note has its number and is a legal document; its value has not been given to the client yet.</summary>
    Issued = 1,

    /// <summary>The note's value has been given to the client, as account credit or as a gateway refund.</summary>
    Applied = 2,

    /// <summary>The note was cancelled. It keeps its number and stays on record; a note is never deleted.</summary>
    Voided = 3,
}
