namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCreditNotePdf;

/// <summary>Query to render one of the calling client's own credit notes as a PDF download.</summary>
/// <remarks>
/// Names no account: whose note it may be is settled from the caller's credential. The staff download that may render any note is
/// a separate use case, <c>GetCreditNotePdfQuery</c>.
/// </remarks>
/// <param name="CreditNoteId">The credit note primary key.</param>
public sealed record GetMyCreditNotePdfQuery(int CreditNoteId);
