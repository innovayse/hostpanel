using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Everything a credit note PDF prints, already resolved, so the renderer draws without reading anything else.</summary>
/// <param name="Number">The note number, printed as the title.</param>
/// <param name="Status">Lifecycle state; a draft or voided note gets a stamp.</param>
/// <param name="IssuedAt">The issue date.</param>
/// <param name="InvoiceId">The credited invoice, printed as its number.</param>
/// <param name="Reason">Why the note was issued.</param>
/// <param name="Seller">The company issuing the note.</param>
/// <param name="BillTo">The client the note is issued to.</param>
/// <param name="LogoPath">The <c>portal.logo</c> setting as stored; null or empty prints no logo.</param>
/// <param name="Lines">The lines credited, in their stored order.</param>
/// <param name="SubTotal">Sum of the line amounts before tax.</param>
/// <param name="TaxRate">Tax rate as a percentage.</param>
/// <param name="Tax">Tax amount.</param>
/// <param name="Total">What the note credits in all.</param>
/// <param name="Money">How amounts in the currency of the note are written.</param>
/// <param name="Labels">The words to print, in the language of the client.</param>
public sealed record CreditNotePdfModel(
    string Number,
    CreditNoteStatus Status,
    DateTimeOffset IssuedAt,
    int InvoiceId,
    string Reason,
    InvoicePdfParty Seller,
    InvoicePdfParty BillTo,
    string? LogoPath,
    IReadOnlyList<InvoicePdfLine> Lines,
    decimal SubTotal,
    decimal TaxRate,
    decimal Tax,
    decimal Total,
    InvoicePdfMoneyFormat Money,
    CreditNotePdfLabels Labels);
