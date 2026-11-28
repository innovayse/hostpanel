using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// Everything one document prints, every text already formatted: the one shape the invoice and the credit note are both
/// reduced to, so the layout is written once.
/// </summary>
/// <param name="Name">The name stored in the document properties, such as <c>Invoice #42</c>.</param>
/// <param name="Title">The large title: the name of the document.</param>
/// <param name="Pill">The status label drawn beside the title.</param>
/// <param name="Meta">The compact block under the title: number, dates, the credited invoice.</param>
/// <param name="Seller">The company issuing the document.</param>
/// <param name="BillTo">The client the document is for.</param>
/// <param name="Headline">The large line above the table, or null for none.</param>
/// <param name="Items">The rows of the items table.</param>
/// <param name="Totals">The rows stacked on the right under the table.</param>
/// <param name="Details">The bank details box, or null for none.</param>
/// <param name="Notes">The paragraphs under the totals.</param>
/// <param name="Closing">The quiet closing line.</param>
/// <param name="Footer">The text on the left of the footer of every page; the page number is on the right.</param>
/// <param name="AccentColour">The brand colour as <c>#rrggbb</c>, or null for neutral greys only.</param>
/// <param name="Labels">The words of the document, for the headings and the footer.</param>
internal sealed record PdfDocumentContent(
    string Name,
    string Title,
    PdfPill Pill,
    IReadOnlyList<PdfRow> Meta,
    InvoicePdfParty Seller,
    InvoicePdfParty BillTo,
    PdfHeadline? Headline,
    IReadOnlyList<PdfItem> Items,
    IReadOnlyList<PdfRow> Totals,
    PdfDetails? Details,
    IReadOnlyList<PdfNote> Notes,
    string Closing,
    string Footer,
    string? AccentColour,
    DocumentPdfLabels Labels);
