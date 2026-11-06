namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNotePdf;

/// <summary>Query to render one credit note as a PDF download: the staff view, and the client view once ownership is settled.</summary>
/// <param name="CreditNoteId">The credit note primary key.</param>
public sealed record GetCreditNotePdfQuery(int CreditNoteId);
