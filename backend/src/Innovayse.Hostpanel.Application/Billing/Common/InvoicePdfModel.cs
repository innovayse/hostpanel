using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Everything an invoice PDF prints, already resolved: names, address lines, the currency's
/// format and the words, so the renderer draws without reading anything else.
/// </summary>
/// <remarks>
/// Deliberately carries no notes. <c>Invoice.Notes</c> is the admins' internal field, and a PDF is a
/// document that gets forwarded to customers and accountants, the admin's copy included.
/// </remarks>
/// <param name="InvoiceId">Invoice primary key, printed as the invoice number.</param>
/// <param name="Status">Lifecycle state, shown as the pill beside the title.</param>
/// <param name="InvoiceDate">Date the invoice was issued.</param>
/// <param name="DueDate">Date payment is due.</param>
/// <param name="Seller">The company issuing the invoice.</param>
/// <param name="BillTo">The client being invoiced.</param>
/// <param name="Lines">Line items in their stored order.</param>
/// <param name="SubTotal">Sum of the line amounts before tax and credit.</param>
/// <param name="TaxRate">Tax rate as a percentage, e.g. 20 for 20%.</param>
/// <param name="Tax">Tax amount.</param>
/// <param name="ClientVatNumber">The client's VAT number when the invoice was raised under the reverse charge, which the document then states; null otherwise.</param>
/// <param name="Credit">Credit applied; the line is left out when zero.</param>
/// <param name="Total">The invoice total, net of credit.</param>
/// <param name="AmountPaid">What the invoice has taken so far; the line is left out when zero.</param>
/// <param name="AmountDue">What is still owed.</param>
/// <param name="Money">How amounts in the invoice's currency are written.</param>
/// <param name="PayUrl">The client portal page of the invoice, linked as "Pay online"; null when the invoice cannot be paid.</param>
/// <param name="BankTransfer">The bank details to pay by transfer, with the invoice's reference; null when the invoice cannot be paid or no transfer is offered.</param>
/// <param name="Labels">The words to print, in the language of the client.</param>
public sealed record InvoicePdfModel(
    int InvoiceId,
    InvoiceStatus Status,
    DateTimeOffset InvoiceDate,
    DateTimeOffset DueDate,
    InvoicePdfSeller Seller,
    InvoicePdfParty BillTo,
    IReadOnlyList<InvoicePdfLine> Lines,
    decimal SubTotal,
    decimal TaxRate,
    decimal Tax,
    string? ClientVatNumber,
    decimal Credit,
    decimal Total,
    decimal AmountPaid,
    decimal AmountDue,
    InvoicePdfMoneyFormat Money,
    string? PayUrl,
    BankTransferDetailsDto? BankTransfer,
    DocumentPdfLabels Labels);
