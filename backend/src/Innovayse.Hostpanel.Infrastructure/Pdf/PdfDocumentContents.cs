using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using PdfSharp.Drawing;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// Reduces an invoice model and a credit note model to the one <see cref="PdfDocumentContent"/> the layout draws: which
/// rows exist, which words they carry, how amounts and dates are written, and which colour the status wears.
/// </summary>
internal static class PdfDocumentContents
{
    /// <summary>The colour of a positive status's text: paid, applied.</summary>
    private static readonly XColor PositiveInk = XColor.FromArgb(0x15, 0x80, 0x3D);

    /// <summary>The background of a positive status.</summary>
    private static readonly XColor PositiveFill = XColor.FromArgb(0xDC, 0xFC, 0xE7);

    /// <summary>The colour of a negative status's text: overdue, in collections.</summary>
    private static readonly XColor NegativeInk = XColor.FromArgb(0xB9, 0x1C, 0x1C);

    /// <summary>The background of a negative status.</summary>
    private static readonly XColor NegativeFill = XColor.FromArgb(0xFE, 0xE2, 0xE2);

    /// <summary>The colour of a waiting status's text: unpaid, payment pending, issued.</summary>
    private static readonly XColor WaitingInk = XColor.FromArgb(0xB4, 0x53, 0x09);

    /// <summary>The background of a waiting status.</summary>
    private static readonly XColor WaitingFill = XColor.FromArgb(0xFE, 0xF3, 0xC7);

    /// <summary>The colour of a closed status's text: draft, cancelled, voided, refunded.</summary>
    private static readonly XColor ClosedInk = XColor.FromArgb(0x4B, 0x55, 0x63);

    /// <summary>The background of a closed status.</summary>
    private static readonly XColor ClosedFill = XColor.FromArgb(0xF3, 0xF4, 0xF6);

    /// <summary>Reduces an invoice.</summary>
    /// <param name="model">Everything the invoice prints, already resolved.</param>
    /// <returns>The content to lay out.</returns>
    public static PdfDocumentContent ForInvoice(InvoicePdfModel model)
    {
        var labels = model.Labels;
        var number = $"#{model.InvoiceId.ToString(CultureInfo.InvariantCulture)}";
        var amountDue = WithCode(model.Money, model.AmountDue);

        var totals = new List<PdfRow>
        {
            new(labels.Subtotal, FormatMoney(model.Money, model.SubTotal)),
            new(TaxLabel(labels, model.TaxRate), FormatMoney(model.Money, model.Tax)),
        };
        if (model.Credit > 0)
        {
            totals.Add(new PdfRow(labels.Credit, "-" + FormatMoney(model.Money, model.Credit)));
        }

        totals.Add(new PdfRow(labels.Total, FormatMoney(model.Money, model.Total), RuleAbove: true));
        if (model.AmountPaid > 0)
        {
            totals.Add(new PdfRow(labels.PaidSoFar, "-" + FormatMoney(model.Money, model.AmountPaid)));
        }

        totals.Add(new PdfRow(labels.AmountDue, amountDue, Strong: true, RuleAbove: true));

        List<PdfNote> notes = [];
        if (model.ClientVatNumber is not null)
        {
            notes.Add(new PdfNote(labels.ReverseCharge, $"{labels.ClientVatNumber}: {model.ClientVatNumber}"));
        }

        return new PdfDocumentContent(
            Name: $"{labels.InvoiceTitle} {number}",
            Title: labels.InvoiceTitle,
            Pill: PillOf(labels.StatusText(model.Status), InvoiceTone(model.Status)),
            Meta:
            [
                new PdfRow(labels.InvoiceNumber, number),
                new PdfRow(labels.DateOfIssue, FormatDate(labels, model.InvoiceDate)),
                new PdfRow(labels.DateDue, FormatDate(labels, model.DueDate)),
            ],
            Seller: model.Seller.Party,
            BillTo: model.BillTo,
            Headline: IsOpen(model.Status) && model.AmountDue > 0
                ? new PdfHeadline(
                    string.Format(CultureInfo.InvariantCulture, labels.DueHeadline, amountDue, FormatDate(labels, model.DueDate)),
                    model.PayUrl is null ? null : labels.PayOnline,
                    model.PayUrl)
                : null,
            Items: ItemsOf(model.Lines, model.Money, model.TaxRate),
            Totals: totals,
            Details: model.BankTransfer is null ? null : DetailsOf(labels, model.BankTransfer),
            Notes: notes,
            Closing: labels.ThankYou,
            Footer: $"{number}  ·  {labels.AmountDue}: {amountDue}",
            AccentColour: model.Seller.AccentColour,
            Labels: labels);
    }

    /// <summary>Reduces a credit note.</summary>
    /// <param name="model">Everything the credit note prints, already resolved.</param>
    /// <returns>The content to lay out.</returns>
    public static PdfDocumentContent ForCreditNote(CreditNotePdfModel model)
    {
        var labels = model.Labels;
        var credited = WithCode(model.Money, model.Total);

        return new PdfDocumentContent(
            Name: $"{labels.CreditNoteTitle} {model.Number}",
            Title: labels.CreditNoteTitle,
            Pill: PillOf(labels.StatusText(model.Status), CreditNoteTone(model.Status)),
            Meta:
            [
                new PdfRow(labels.CreditNoteNumber, model.Number),
                new PdfRow(labels.DateOfIssue, FormatDate(labels, model.IssuedAt)),
                new PdfRow(labels.CreditedInvoice, $"#{model.InvoiceId.ToString(CultureInfo.InvariantCulture)}"),
            ],
            Seller: model.Seller.Party,
            BillTo: model.BillTo,
            Headline: new PdfHeadline(string.Format(CultureInfo.InvariantCulture, labels.CreditedHeadline, credited), null, null),
            Items: ItemsOf(model.Lines, model.Money, model.TaxRate),
            Totals:
            [
                new PdfRow(labels.Subtotal, FormatMoney(model.Money, model.SubTotal)),
                new PdfRow(TaxLabel(labels, model.TaxRate), FormatMoney(model.Money, model.Tax)),
                new PdfRow(labels.TotalCredited, credited, Strong: true, RuleAbove: true),
            ],
            Details: null,
            Notes: [new PdfNote(labels.Reason, model.Reason)],
            Closing: labels.ThankYou,
            Footer: $"{model.Number}  ·  {labels.TotalCredited}: {credited}",
            AccentColour: model.Seller.AccentColour,
            Labels: labels);
    }

    /// <summary>Writes an amount in the document's currency.</summary>
    /// <remarks>
    /// Digits and grouping are invariant (<c>1,234.50</c>); the prefix, suffix and decimal places are the currency's own.
    /// </remarks>
    /// <param name="money">How the currency is written.</param>
    /// <param name="amount">The amount; rounded to the currency's decimal places.</param>
    /// <returns>The prefix, the grouped amount and the suffix.</returns>
    private static string FormatMoney(InvoicePdfMoneyFormat money, decimal amount) =>
        money.Prefix
        + amount.ToString("N" + money.Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        + money.Suffix;

    /// <summary>Writes an amount with the currency code after it, unless the currency's own symbols already carry the code.</summary>
    /// <param name="money">How the currency is written.</param>
    /// <param name="amount">The amount.</param>
    /// <returns>The amount, such as <c>$15.00 USD</c>.</returns>
    private static string WithCode(InvoicePdfMoneyFormat money, decimal amount)
    {
        var text = FormatMoney(money, amount);
        return text.Contains(money.Code, StringComparison.Ordinal) ? text : $"{text} {money.Code}";
    }

    /// <summary>Writes a date in the language of the document.</summary>
    /// <param name="labels">The words of the document, which name the culture and the format of its dates.</param>
    /// <param name="date">The date.</param>
    /// <returns>The date with its month spelled out in the document's language.</returns>
    private static string FormatDate(DocumentPdfLabels labels, DateTimeOffset date) =>
        date.ToString(labels.DateFormat, CultureInfo.GetCultureInfo(labels.Culture));

    /// <summary>Words the tax line: the tax word and the rate.</summary>
    /// <param name="labels">The words of the document.</param>
    /// <param name="taxRate">The tax rate as a percentage.</param>
    /// <returns>The label, such as <c>Tax (20%)</c>.</returns>
    private static string TaxLabel(DocumentPdfLabels labels, decimal taxRate) => $"{labels.Tax} ({RateText(taxRate)})";

    /// <summary>Writes a tax rate.</summary>
    /// <param name="taxRate">The tax rate as a percentage.</param>
    /// <returns>The rate with at most two decimals and a percent sign, such as <c>7.5%</c>.</returns>
    private static string RateText(decimal taxRate) => taxRate.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>Formats the rows of the items table.</summary>
    /// <param name="lines">The lines in their stored order.</param>
    /// <param name="money">How the currency is written.</param>
    /// <param name="taxRate">The tax rate every line is taxed at.</param>
    /// <returns>One formatted row per line.</returns>
    private static List<PdfItem> ItemsOf(IReadOnlyList<InvoicePdfLine> lines, InvoicePdfMoneyFormat money, decimal taxRate) =>
        [.. lines.Select(line => new PdfItem(
            line.Description,
            line.Quantity.ToString(CultureInfo.InvariantCulture),
            FormatMoney(money, line.UnitPrice),
            RateText(taxRate),
            FormatMoney(money, line.Amount)))];

    /// <summary>Lays the bank details out as rows, each only when the operator filled it in, and always the reference.</summary>
    /// <param name="labels">The words of the document.</param>
    /// <param name="details">The bank details and the invoice's reference.</param>
    /// <returns>The box.</returns>
    private static PdfDetails DetailsOf(DocumentPdfLabels labels, BankTransferDetailsDto details)
    {
        List<PdfRow> rows = [];
        AddRow(rows, labels.AccountName, details.AccountName);
        AddRow(rows, labels.Iban, details.Iban);
        AddRow(rows, labels.Swift, details.Swift);
        AddRow(rows, labels.Bank, details.BankName);
        AddRow(rows, labels.Reference, details.Reference);
        return new PdfDetails(labels.BankTransfer, rows);
    }

    /// <summary>Adds a row when its value is set.</summary>
    /// <param name="rows">The rows so far.</param>
    /// <param name="label">The label of the row.</param>
    /// <param name="value">The value; nothing is added when it is null.</param>
    private static void AddRow(List<PdfRow> rows, string label, string? value)
    {
        if (value is not null)
        {
            rows.Add(new PdfRow(label, value));
        }
    }

    /// <summary>Tells whether an invoice is still waiting for its money, which is when its headline asks for it.</summary>
    /// <param name="status">The invoice status.</param>
    /// <returns><see langword="true"/> for an unpaid, overdue, pending or collections invoice.</returns>
    private static bool IsOpen(InvoiceStatus status) =>
        status is InvoiceStatus.Unpaid or InvoiceStatus.Overdue or InvoiceStatus.PaymentPending or InvoiceStatus.Collections;

    /// <summary>Picks the colours of an invoice status.</summary>
    /// <param name="status">The invoice status.</param>
    /// <returns>Green for paid, red for overdue or in collections, amber for unpaid or pending, grey for the rest.</returns>
    private static (XColor Ink, XColor Fill) InvoiceTone(InvoiceStatus status) =>
        status switch
        {
            InvoiceStatus.Paid => (PositiveInk, PositiveFill),
            InvoiceStatus.Overdue or InvoiceStatus.Collections => (NegativeInk, NegativeFill),
            InvoiceStatus.Unpaid or InvoiceStatus.PaymentPending => (WaitingInk, WaitingFill),
            _ => (ClosedInk, ClosedFill),
        };

    /// <summary>Picks the colours of a credit note status.</summary>
    /// <param name="status">The credit note status.</param>
    /// <returns>Green for applied, amber for issued, grey for a draft or a voided note.</returns>
    private static (XColor Ink, XColor Fill) CreditNoteTone(CreditNoteStatus status) =>
        status switch
        {
            CreditNoteStatus.Applied => (PositiveInk, PositiveFill),
            CreditNoteStatus.Issued => (WaitingInk, WaitingFill),
            _ => (ClosedInk, ClosedFill),
        };

    /// <summary>Builds the status label.</summary>
    /// <param name="text">The status words.</param>
    /// <param name="tone">The text and background colours.</param>
    /// <returns>The pill.</returns>
    private static PdfPill PillOf(string text, (XColor Ink, XColor Fill) tone) => new(text, tone.Ink, tone.Fill);
}
