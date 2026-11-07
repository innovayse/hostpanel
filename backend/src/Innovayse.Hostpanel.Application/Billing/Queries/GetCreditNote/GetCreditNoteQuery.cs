namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNote;

/// <summary>Query for one credit note with its lines: staff view.</summary>
/// <param name="CreditNoteId">The credit note primary key.</param>
public sealed record GetCreditNoteQuery(int CreditNoteId);
