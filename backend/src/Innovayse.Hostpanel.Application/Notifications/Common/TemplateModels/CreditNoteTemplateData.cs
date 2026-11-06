namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>A credit note as the credit note email reads it.</summary>
/// <param name="Number">The note number such as <c>CN-0001</c>, read as <c>number</c>.</param>
/// <param name="Total">What the note credits in all, tax included, read as <c>total</c>.</param>
/// <param name="Currency">The ISO 4217 code the total is in, read as <c>currency</c>.</param>
/// <param name="InvoiceId">The credited invoice, read as <c>invoice_id</c>.</param>
/// <param name="Url">The absolute link to the credit notes page in the client area, read as <c>url</c>.</param>
public sealed record CreditNoteTemplateData(string Number, decimal Total, string Currency, int InvoiceId, string Url);
