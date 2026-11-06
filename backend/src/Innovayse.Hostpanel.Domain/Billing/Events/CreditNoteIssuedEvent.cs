using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when a credit note's value has been given to the client (<see cref="CreditNote.MarkApplied"/>), so the client is
/// sent the note by email.
/// </summary>
/// <param name="CreditNoteId">The credit note.</param>
/// <param name="ClientId">The client it was issued to.</param>
/// <param name="Number">The note's number, for example <c>CN-0001</c>.</param>
/// <param name="InvoiceId">The invoice it credits.</param>
/// <param name="Total">The note's total, tax included.</param>
/// <param name="Currency">ISO 4217 code the total is in.</param>
public sealed record CreditNoteIssuedEvent(int CreditNoteId, int ClientId, string Number, int InvoiceId, decimal Total, string Currency)
    : IDomainEvent
{
    /// <summary>When the note was applied, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
