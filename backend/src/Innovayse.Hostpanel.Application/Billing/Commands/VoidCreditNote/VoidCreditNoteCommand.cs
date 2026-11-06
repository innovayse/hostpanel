namespace Innovayse.Hostpanel.Application.Billing.Commands.VoidCreditNote;

/// <summary>
/// Command for staff to void a credit note. A note is never deleted: it keeps its number and stays on record as voided.
/// </summary>
/// <param name="CreditNoteId">The note to void; the route id wins over any value in the body.</param>
/// <param name="Reason">Why the note is voided; kept on the note.</param>
public sealed record VoidCreditNoteCommand(int CreditNoteId, string Reason);
