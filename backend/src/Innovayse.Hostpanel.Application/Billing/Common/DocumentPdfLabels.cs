using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Every word an invoice or a credit note PDF prints, in one language: the words both documents share, the invoice's own
/// and the credit note's own. <see cref="DocumentPdfLabelSets"/> holds one set per language and picks the one a client reads.
/// </summary>
public sealed record DocumentPdfLabels
{
    /// <summary>Gets the ISO 639-1 code of the language, which also names the localized settings rows the document reads.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the culture dates are written in, such as <c>ru-RU</c>.</summary>
    public required string Culture { get; init; }

    /// <summary>Gets the custom date format the culture's month names are written with.</summary>
    public required string DateFormat { get; init; }

    /// <summary>Gets the heading above the seller.</summary>
    public required string From { get; init; }

    /// <summary>Gets the heading above the client.</summary>
    public required string BillTo { get; init; }

    /// <summary>Gets the items table heading for the description column.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the items table heading for the quantity column.</summary>
    public required string Quantity { get; init; }

    /// <summary>Gets the items table heading for the unit price column.</summary>
    public required string UnitPrice { get; init; }

    /// <summary>Gets the word for tax: the items table heading and the tax line of the totals, before the percentage.</summary>
    public required string Tax { get; init; }

    /// <summary>Gets the items table heading for the amount column.</summary>
    public required string Amount { get; init; }

    /// <summary>Gets the label of the sub total.</summary>
    public required string Subtotal { get; init; }

    /// <summary>Gets the label of the credit line.</summary>
    public required string Credit { get; init; }

    /// <summary>Gets the label of the total.</summary>
    public required string Total { get; init; }

    /// <summary>Gets the footer word before the page number.</summary>
    public required string Page { get; init; }

    /// <summary>Gets the footer word between the page number and the page count.</summary>
    public required string Of { get; init; }

    /// <summary>Gets the prefix of the seller's tax identifier line.</summary>
    public required string TaxId { get; init; }

    /// <summary>Gets the prefix of the seller's registration number line.</summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>Gets the prefix of the client's VAT number line.</summary>
    public required string VatNumber { get; init; }

    /// <summary>Gets the closing line under the document.</summary>
    public required string ThankYou { get; init; }

    /// <summary>Gets the name of an invoice, printed as the title.</summary>
    public required string InvoiceTitle { get; init; }

    /// <summary>Gets the label of the invoice number.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>Gets the label of the date the document was issued.</summary>
    public required string DateOfIssue { get; init; }

    /// <summary>Gets the label of the date payment is due.</summary>
    public required string DateDue { get; init; }

    /// <summary>Gets the headline of an unpaid invoice, with <c>{0}</c> for the amount and <c>{1}</c> for the due date.</summary>
    public required string DueHeadline { get; init; }

    /// <summary>Gets the text of the link to the invoice page.</summary>
    public required string PayOnline { get; init; }

    /// <summary>Gets the label of what has been paid already.</summary>
    public required string PaidSoFar { get; init; }

    /// <summary>Gets the label of what is still owed.</summary>
    public required string AmountDue { get; init; }

    /// <summary>Gets the heading of the bank transfer details.</summary>
    public required string BankTransfer { get; init; }

    /// <summary>Gets the label of the account holder.</summary>
    public required string AccountName { get; init; }

    /// <summary>Gets the label of the IBAN.</summary>
    public required string Iban { get; init; }

    /// <summary>Gets the label of the SWIFT or BIC code.</summary>
    public required string Swift { get; init; }

    /// <summary>Gets the label of the bank's name.</summary>
    public required string Bank { get; init; }

    /// <summary>Gets the label of the reference to write on the transfer.</summary>
    public required string Reference { get; init; }

    /// <summary>Gets the sentence an invoice raised under the reverse charge carries.</summary>
    public required string ReverseCharge { get; init; }

    /// <summary>Gets the label of the client's VAT number in the reverse charge statement.</summary>
    public required string ClientVatNumber { get; init; }

    /// <summary>Gets the status text of a draft invoice or credit note.</summary>
    public required string StatusDraft { get; init; }

    /// <summary>Gets the status text of an unpaid invoice.</summary>
    public required string StatusUnpaid { get; init; }

    /// <summary>Gets the status text of a paid invoice.</summary>
    public required string StatusPaid { get; init; }

    /// <summary>Gets the status text of an overdue invoice.</summary>
    public required string StatusOverdue { get; init; }

    /// <summary>Gets the status text of a cancelled invoice.</summary>
    public required string StatusCancelled { get; init; }

    /// <summary>Gets the status text of a refunded invoice.</summary>
    public required string StatusRefunded { get; init; }

    /// <summary>Gets the status text of an invoice sent to collections.</summary>
    public required string StatusCollections { get; init; }

    /// <summary>Gets the status text of an invoice whose payment is awaiting confirmation.</summary>
    public required string StatusPaymentPending { get; init; }

    /// <summary>Gets the status text of a partially refunded invoice.</summary>
    public required string StatusPartiallyRefunded { get; init; }

    /// <summary>Gets the name of a credit note, printed as the title.</summary>
    public required string CreditNoteTitle { get; init; }

    /// <summary>Gets the label of the credit note number.</summary>
    public required string CreditNoteNumber { get; init; }

    /// <summary>Gets the label of the invoice a credit note credits.</summary>
    public required string CreditedInvoice { get; init; }

    /// <summary>Gets the label of the reason paragraph.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the label of the total, in place of the plain total.</summary>
    public required string TotalCredited { get; init; }

    /// <summary>Gets the headline of a credit note, with <c>{0}</c> for the amount credited.</summary>
    public required string CreditedHeadline { get; init; }

    /// <summary>Gets the status text of an issued credit note.</summary>
    public required string StatusIssued { get; init; }

    /// <summary>Gets the status text of an applied credit note.</summary>
    public required string StatusApplied { get; init; }

    /// <summary>Gets the status text of a voided credit note.</summary>
    public required string StatusVoided { get; init; }

    /// <summary>Gets the words of an invoice status.</summary>
    /// <param name="status">The invoice status.</param>
    /// <returns>The status text in this language.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not one the document has a text for.</exception>
    public string StatusText(InvoiceStatus status) =>
        status switch
        {
            InvoiceStatus.Draft => StatusDraft,
            InvoiceStatus.Unpaid => StatusUnpaid,
            InvoiceStatus.Paid => StatusPaid,
            InvoiceStatus.Overdue => StatusOverdue,
            InvoiceStatus.Cancelled => StatusCancelled,
            InvoiceStatus.Refunded => StatusRefunded,
            InvoiceStatus.Collections => StatusCollections,
            InvoiceStatus.PaymentPending => StatusPaymentPending,
            InvoiceStatus.PartiallyRefunded => StatusPartiallyRefunded,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The invoice status has no text."),
        };

    /// <summary>Gets the words of a credit note status.</summary>
    /// <param name="status">The credit note status.</param>
    /// <returns>The status text in this language.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not one the document has a text for.</exception>
    public string StatusText(CreditNoteStatus status) =>
        status switch
        {
            CreditNoteStatus.Draft => StatusDraft,
            CreditNoteStatus.Issued => StatusIssued,
            CreditNoteStatus.Applied => StatusApplied,
            CreditNoteStatus.Voided => StatusVoided,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The credit note status has no text."),
        };
}
