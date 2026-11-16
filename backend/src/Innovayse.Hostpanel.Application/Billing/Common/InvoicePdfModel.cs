using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Everything an invoice PDF prints, already resolved: names, address lines and the currency's
/// format, so the renderer draws without reading anything else.
/// </summary>
/// <remarks>
/// Deliberately carries no notes. <c>Invoice.Notes</c> is the admins' internal field, and a PDF is a
/// document that gets forwarded to customers and accountants, the admin's copy included.
/// </remarks>
/// <param name="InvoiceId">Invoice primary key, printed as the invoice number.</param>
/// <param name="Status">Lifecycle state; a paid, draft, cancelled or refunded invoice gets a stamp.</param>
/// <param name="InvoiceDate">Date the invoice was issued.</param>
/// <param name="DueDate">Date payment is due.</param>
/// <param name="PaidAt">When the invoice was paid, printed on the PAID stamp; null when unpaid.</param>
/// <param name="Seller">The company issuing the invoice.</param>
/// <param name="BillTo">The client being invoiced.</param>
/// <param name="LogoPath">The <c>portal.logo</c> setting as stored; null or empty prints no logo.</param>
/// <param name="Lines">Line items in their stored order.</param>
/// <param name="SubTotal">Sum of the line amounts before tax and credit.</param>
/// <param name="TaxRate">Tax rate as a percentage, e.g. 20 for 20%.</param>
/// <param name="Tax">Tax amount.</param>
/// <param name="ClientVatNumber">The client's VAT number when the invoice was raised under the reverse charge, which the document then states; null otherwise.</param>
/// <param name="Credit">Credit applied; the line is left out when zero.</param>
/// <param name="Total">Amount due.</param>
/// <param name="Money">How amounts in the invoice's currency are written.</param>
public sealed record InvoicePdfModel(
    int InvoiceId,
    InvoiceStatus Status,
    DateTimeOffset InvoiceDate,
    DateTimeOffset DueDate,
    DateTimeOffset? PaidAt,
    InvoicePdfParty Seller,
    InvoicePdfParty BillTo,
    string? LogoPath,
    IReadOnlyList<InvoicePdfLine> Lines,
    decimal SubTotal,
    decimal TaxRate,
    decimal Tax,
    string? ClientVatNumber,
    decimal Credit,
    decimal Total,
    InvoicePdfMoneyFormat Money);
