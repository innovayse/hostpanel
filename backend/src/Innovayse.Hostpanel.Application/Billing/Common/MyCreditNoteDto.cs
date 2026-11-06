using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A credit note as the client it was issued to sees it. Drafts are never shown to a client.</summary>
/// <param name="Id">Credit note primary key.</param>
/// <param name="Number">The note number such as <c>CN-0001</c>.</param>
/// <param name="Status">Issued, Applied or Voided.</param>
/// <param name="Method">How the value went back to the client.</param>
/// <param name="InvoiceId">The invoice the note credits.</param>
/// <param name="Currency">ISO 4217 code every amount is in.</param>
/// <param name="SubTotal">Sum of the lines before tax.</param>
/// <param name="TaxRate">Tax percentage.</param>
/// <param name="Tax">Tax on the sub total.</param>
/// <param name="Total">What the note credits in all.</param>
/// <param name="Reason">Why the note was issued.</param>
/// <param name="IssuedAt">When the note was issued.</param>
/// <param name="VoidedAt">When it was voided; null while it stands.</param>
public sealed record MyCreditNoteDto(
    int Id,
    string Number,
    CreditNoteStatus Status,
    CreditNoteMethod Method,
    int InvoiceId,
    string Currency,
    decimal SubTotal,
    decimal TaxRate,
    decimal Tax,
    decimal Total,
    string Reason,
    DateTimeOffset IssuedAt,
    DateTimeOffset? VoidedAt);
