using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// Lays invoices out as A4 PDFs with MigraDoc, rendered through PDFsharp.
/// </summary>
/// <remarks>
/// <para>
/// MigraDoc does the layout: wrapping, the items table with its header repeated on every page it
/// runs onto, page breaks and the page-number footer. The PAID, DRAFT, CANCELLED or REFUNDED stamp is the one thing
/// drawn afterwards, straight onto the first page with PDFsharp, because MigraDoc cannot rotate
/// text. Text is set as text, so the PDF stays searchable and selectable.
/// </para>
/// <para>
/// The logo is read only from the branding upload directory under the web root. A
/// <c>portal.logo</c> that is a URL, or a path that leads anywhere else, is left out: rendering
/// never reaches the network and never opens a file outside that directory.
/// </para>
/// </remarks>
public sealed partial class MigraDocInvoicePdfRenderer : IInvoicePdfRenderer
{
    /// <summary>A4 width in points.</summary>
    private const double PageWidth = 595;

    /// <summary>A4 height in points.</summary>
    private const double PageHeight = 842;

    /// <summary>Margin on the top and sides, in points.</summary>
    private const double Margin = 40;

    /// <summary>Space kept above the bottom margin for the page number.</summary>
    private const double FooterHeight = 20;

    /// <summary>Size of body text.</summary>
    private const double BodySize = 9;

    /// <summary>Size of labels and the page number.</summary>
    private const double SmallSize = 8;

    /// <summary>Size of party names and the total.</summary>
    private const double EmphasisSize = 11;

    /// <summary>Size of the invoice number in the header.</summary>
    private const double TitleSize = 18;

    /// <summary>Size of the stamp.</summary>
    private const double StampSize = 40;

    /// <summary>Rotation of the stamp, in degrees.</summary>
    private const double StampAngle = -20;

    /// <summary>Vertical centre of the stamp: across the table on a typical first page.</summary>
    private const double StampCentreY = PageHeight * 0.385;

    /// <summary>Thickness of table rules.</summary>
    private const double RuleWidth = 0.75;

    /// <summary>Space below the logo and between the totals rule and the total.</summary>
    private const double RuleGap = 6;

    /// <summary>Space above and below the text of a table row.</summary>
    private const double RowPadding = 3;

    /// <summary>Space between the header and the bill-to block.</summary>
    private const double SectionGap = 24;

    /// <summary>Space between the bill-to block and the items table.</summary>
    private const double TableGap = 20;

    /// <summary>Space between the items table and the totals.</summary>
    private const double TotalsGap = 12;

    /// <summary>Space between the invoice number and the dates below it.</summary>
    private const double TitleGap = 8;

    /// <summary>Width kept for a date or status value; its label sits to the left of it.</summary>
    private const double HeaderValueWidth = 80;

    /// <summary>Width of the invoice number, dates and status block on the right of the header.</summary>
    private const double HeaderRightBlockWidth = 200;

    /// <summary>Width of the page between the margins.</summary>
    private const double ContentWidth = PageWidth - (2 * Margin);

    /// <summary>Width of the Qty column.</summary>
    private const double QtyWidth = 50;

    /// <summary>Width of each money column.</summary>
    private const double MoneyWidth = 90;

    /// <summary>Gap kept between columns: description and Qty, seller and header block.</summary>
    private const double ColumnGap = 8;

    /// <summary>
    /// Width of the description column, the narrowest that customer-supplied text goes into. A
    /// word wider than this cannot fit on a line anywhere it is printed.
    /// </summary>
    private const double DescriptionWidth = ContentWidth - QtyWidth - (2 * MoneyWidth) - ColumnGap;

    /// <summary>Shortest word worth measuring: nothing shorter is near <see cref="DescriptionWidth"/>.</summary>
    private const int LongWordLength = 20;

    /// <summary>A break opportunity that prints nothing; MigraDoc breaks a line there when it must.</summary>
    private const char ZeroWidthSpace = '\u200B';

    /// <summary>Width of the totals block at the bottom right.</summary>
    private const double TotalsWidth = 220;

    /// <summary>Largest width the logo is drawn at.</summary>
    private const double LogoMaxWidth = 160;

    /// <summary>Largest height the logo is drawn at.</summary>
    private const double LogoMaxHeight = 60;

    /// <summary>The date format every date on the invoice uses.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Text colour.</summary>
    private static readonly Color Ink = new(0x1F, 0x29, 0x37);

    /// <summary>Colour of labels and secondary text.</summary>
    private static readonly Color Muted = new(0x6B, 0x72, 0x80);

    /// <summary>Colour of table rules.</summary>
    private static readonly Color Rule = new(0xD1, 0xD5, 0xDB);

    /// <summary>Colour of the PAID stamp, translucent so the page shows through.</summary>
    private static readonly XColor PaidStamp = XColor.FromArgb(0x55, 0x16, 0xA3, 0x4A);

    /// <summary>Colour of the DRAFT stamp, translucent so the page shows through.</summary>
    private static readonly XColor DraftStamp = XColor.FromArgb(0x55, 0x6B, 0x72, 0x80);

    /// <summary>Colour of the CANCELLED stamp, translucent so the page shows through.</summary>
    private static readonly XColor CancelledStamp = XColor.FromArgb(0x55, 0xDC, 0x26, 0x26);

    /// <summary>Colour of the REFUNDED and PARTIALLY REFUNDED stamps, translucent so the page shows through.</summary>
    private static readonly XColor RefundedStamp = XColor.FromArgb(0x55, 0xD9, 0x77, 0x06);

    /// <summary>Where the web root is, for finding an uploaded logo.</summary>
    private readonly IWebHostEnvironment _env;

    /// <summary>Where branding uploads live under the web root.</summary>
    private readonly BrandingOptions _branding;

    /// <summary>The process-wide limit on renders in flight.</summary>
    private readonly InvoicePdfRenderGate _gate;

    /// <summary>Logs why a configured logo was left out, and a render turned away as busy.</summary>
    private readonly ILogger<MigraDocInvoicePdfRenderer> _logger;

    /// <summary>Initializes the renderer.</summary>
    /// <param name="env">Where the web root is, for finding an uploaded logo.</param>
    /// <param name="branding">Where branding uploads live under the web root.</param>
    /// <param name="gate">The process-wide limit on renders in flight.</param>
    /// <param name="logger">Logs why a configured logo was left out, and a render turned away as busy.</param>
    public MigraDocInvoicePdfRenderer(
        IWebHostEnvironment env,
        IOptions<BrandingOptions> branding,
        InvoicePdfRenderGate gate,
        ILogger<MigraDocInvoicePdfRenderer> logger)
    {
        _env = env;
        _branding = branding.Value;
        _gate = gate;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Waits for a slot in <see cref="InvoicePdfRenderGate"/> first, then renders inline and holds
    /// the slot until the bytes are written; a render that throws gives its slot back too. When this
    /// request gets no slot within the gate's wait, nothing is rendered and the result is
    /// <c>INVOICE_PDF_RENDER_BUSY</c>, logged at Warning so a deployment that keeps hitting the cap
    /// can see it.
    /// </remarks>
    public Task<Result<byte[]>> RenderAsync(InvoicePdfModel model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderInSlotAsync(() => Render(model), ct);
    }

    /// <inheritdoc/>
    /// <remarks>Takes a slot in the same gate as an invoice render, so credit notes and invoices share one limit.</remarks>
    public Task<Result<byte[]>> RenderCreditNoteAsync(CreditNotePdfModel model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderInSlotAsync(() => RenderCreditNote(model), ct);
    }

    /// <summary>Waits for a render slot, then runs one render inside it.</summary>
    /// <param name="render">Draws the document; runs only once a slot is held.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF bytes, or <c>INVOICE_PDF_RENDER_BUSY</c> when no slot came free in time.</returns>
    private async Task<Result<byte[]>> RenderInSlotAsync(Func<byte[]> render, CancellationToken ct)
    {
        using var slot = await _gate.TryEnterAsync(ct);
        if (slot is null)
        {
            _logger.LogWarning(
                "Invoice PDF render refused as busy: it waited {WaitSeconds}s and no slot came free, with {Limit} render(s) allowed at once. Raise InvoicePdf__MaxConcurrentRenders if this keeps happening.",
                _gate.MaxWait.TotalSeconds,
                _gate.Limit);
            return Result<byte[]>.FailureCode(HostpanelErrorCodes.INVOICE_PDF_RENDER_BUSY);
        }

        return Result<byte[]>.Success(render());
    }

    /// <summary>Lays out and renders the invoice.</summary>
    /// <param name="model">Everything the PDF prints, already resolved.</param>
    /// <returns>The PDF file's bytes.</returns>
    private byte[] Render(InvoicePdfModel model)
    {
        var document = NewDocument($"Invoice #{model.InvoiceId}", model.Seller.Name, out var section);
        var labels = DocumentPdfLabels.English;

        AddHeader(
            section,
            model.Seller,
            LoadLogo(model.LogoPath),
            $"Invoice #{model.InvoiceId}",
            [
                ("Invoice date", model.InvoiceDate.ToString(DateFormat, CultureInfo.InvariantCulture)),
                ("Due date", model.DueDate.ToString(DateFormat, CultureInfo.InvariantCulture)),
                ("Status", StatusText(model.Status.ToString())),
            ]);
        AddParty(section.Elements, labels.BillTo, model.BillTo).Format.SpaceBefore = SectionGap;
        AddItems(section, model.Lines, model.Money, labels);
        AddTotals(section, model.Money, model.SubTotal, model.TaxRate, model.Tax, model.Credit, model.Total, labels, labels.Total);
        AddFooter(section, labels);

        return Save(document, page => DrawStamp(page, InvoiceStamp(model)));
    }

    /// <summary>Lays out and renders a credit note in the language of its labels.</summary>
    /// <param name="model">Everything the PDF prints, already resolved.</param>
    /// <returns>The PDF file's bytes.</returns>
    private byte[] RenderCreditNote(CreditNotePdfModel model)
    {
        var labels = model.Labels;
        var document = NewDocument($"{labels.Title} {model.Number}", model.Seller.Name, out var section);

        AddHeader(
            section,
            model.Seller,
            LoadLogo(model.LogoPath),
            $"{labels.Title} {model.Number}",
            [
                (labels.IssueDate, model.IssuedAt.ToString(DateFormat, CultureInfo.InvariantCulture)),
                (labels.Invoice, $"#{model.InvoiceId}"),
                (labels.Status, CreditNoteStatusText(model.Status, labels)),
            ]);
        AddParty(section.Elements, labels.Document.BillTo, model.BillTo).Format.SpaceBefore = SectionGap;
        AddItems(section, model.Lines, model.Money, labels.Document);
        AddTotals(section, model.Money, model.SubTotal, model.TaxRate, model.Tax, 0m, model.Total, labels.Document, labels.TotalCredited);

        // The reason is printed on the note itself: the client reads why they were credited.
        AddText(section.Elements, labels.Reason, bold: true, SmallSize, Muted).Format.SpaceBefore = SectionGap;
        AddText(section.Elements, model.Reason);
        AddFooter(section, labels.Document);

        return Save(document, page => DrawStamp(page, CreditNoteStamp(model)));
    }

    /// <summary>Starts an A4 document with the panel font, margins and footer room.</summary>
    /// <param name="title">The title stored in the document properties.</param>
    /// <param name="author">The author stored in the document properties: the seller.</param>
    /// <param name="section">Receives the one section the content is added to.</param>
    /// <returns>The new document.</returns>
    private static Document NewDocument(string title, string author, out Section section)
    {
        PdfFonts.EnsureInitialized();

        var document = new Document();
        document.Info.Title = title;
        document.Info.Author = author;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.Primary;
        normal.Font.Size = BodySize;
        normal.Font.Color = Ink;

        section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageWidth = PageWidth;
        setup.PageHeight = PageHeight;
        setup.TopMargin = Margin;
        setup.LeftMargin = Margin;
        setup.RightMargin = Margin;
        setup.BottomMargin = Margin + FooterHeight;
        setup.FooterDistance = Margin - SmallSize;
        return document;
    }

    /// <summary>Renders the built document, lets the caller stamp its first page, and returns the file's bytes.</summary>
    /// <param name="document">The finished document.</param>
    /// <param name="stampFirstPage">Draws the stamp onto the rendered first page.</param>
    /// <returns>The PDF file's bytes.</returns>
    private static byte[] Save(Document document, Action<PdfSharp.Pdf.PdfPage> stampFirstPage)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var pdf = renderer.PdfDocument;
        stampFirstPage(pdf.Pages[0]);

        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    /// <summary>
    /// Adds a paragraph of text, setting any character the main font lacks in the fallback font and
    /// printing a placeholder for any character neither has.
    /// </summary>
    /// <param name="elements">Where the paragraph goes: a section's or a cell's elements.</param>
    /// <param name="text">The text to set.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="color">Colour; the body text colour when null.</param>
    /// <param name="alignment">Horizontal alignment.</param>
    /// <returns>The paragraph, for further formatting.</returns>
    private static Paragraph AddText(
        DocumentElements elements,
        string text,
        bool bold = false,
        double size = BodySize,
        Color? color = null,
        ParagraphAlignment alignment = ParagraphAlignment.Left)
    {
        var paragraph = elements.AddParagraph();
        paragraph.Format.Font.Bold = bold;
        paragraph.Format.Font.Size = size;
        paragraph.Format.Font.Color = color ?? Ink;
        paragraph.Format.Alignment = alignment;
        var printable = BreakLongWords(PdfFonts.ReplaceUncovered(text, bold), bold, size);
        foreach (var (run, useFallback) in PdfFonts.Runs(printable, bold))
        {
            var formatted = paragraph.AddFormattedText(run);
            if (useFallback)
            {
                formatted.Font.Name = PdfFonts.Fallback;
            }
        }

        return paragraph;
    }

    /// <summary>
    /// Lets MigraDoc break a word too wide for any column: it only breaks lines at spaces, so a
    /// long unbroken word (a hostname, a pasted reference) would otherwise run off the page.
    /// </summary>
    /// <remarks>
    /// Such a word gets a zero-width space between its characters, never before a combining mark.
    /// Words that fit are left alone, so they still wrap whole.
    /// </remarks>
    /// <param name="text">The text to set.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    /// <param name="size">Font size in points.</param>
    /// <returns>The text, with break opportunities inside any word too wide to fit.</returns>
    private static string BreakLongWords(string text, bool bold, double size)
    {
        var words = text.Split(' ');
        if (words.All(w => w.Length < LongWordLength))
        {
            return text;
        }

        using var measure = XGraphics.CreateMeasureContext(new XSize(PageWidth, PageHeight), XGraphicsUnit.Point, XPageDirection.Downwards);
        var font = new XFont(PdfFonts.Primary, size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length < LongWordLength || measure.MeasureString(words[i], font).Width <= DescriptionWidth)
            {
                continue;
            }

            var breakable = new StringBuilder();
            foreach (var rune in words[i].EnumerateRunes())
            {
                var category = Rune.GetUnicodeCategory(rune);
                if (breakable.Length > 0 && category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
                {
                    breakable.Append(ZeroWidthSpace);
                }

                breakable.Append(rune.ToString());
            }

            words[i] = breakable.ToString();
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// Adds the logo and seller on the left and the invoice number, dates and status on the right,
    /// as one borderless table row so a long seller address wraps beside the dates instead of over
    /// them, and whatever follows starts below the taller side.
    /// </summary>
    /// <param name="section">The section being built.</param>
    /// <param name="seller">The company issuing the document.</param>
    /// <param name="logo">The logo and the size to draw it at, or null for none.</param>
    /// <param name="title">The document name and number, set large on the right.</param>
    /// <param name="rows">The label and value pairs set under the title: dates, the credited invoice, the status.</param>
    private static void AddHeader(
        Section section, InvoicePdfParty seller, PdfLogo? logo, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        var table = section.AddTable();
        table.LeftPadding = 0;
        table.RightPadding = 0;
        table.TopPadding = 0;
        table.BottomPadding = 0;
        table.AddColumn(ContentWidth - HeaderRightBlockWidth).RightPadding = ColumnGap;
        table.AddColumn(HeaderRightBlockWidth);
        var row = table.AddRow();

        var sellerCell = row.Cells[0];
        if (logo is not null)
        {
            var picture = sellerCell.AddImage("base64:" + Convert.ToBase64String(logo.Bytes));
            picture.LockAspectRatio = false;
            picture.Width = logo.Width;
            picture.Height = logo.Height;
        }

        AddParty(sellerCell.Elements, null, seller, firstSpaceBefore: logo is null ? 0 : RuleGap);

        var details = row.Cells[1].Elements;
        AddText(details, title, bold: true, TitleSize, alignment: ParagraphAlignment.Right)
            .Format.SpaceAfter = TitleGap;
        foreach (var (label, value) in rows)
        {
            // Right-aligned tab stops: the label ends where the value column starts, the value at the edge.
            var paragraph = details.AddParagraph();
            paragraph.Format.TabStops.AddTabStop(HeaderRightBlockWidth - HeaderValueWidth, TabAlignment.Right);
            paragraph.Format.TabStops.AddTabStop(HeaderRightBlockWidth, TabAlignment.Right);
            paragraph.AddTab();
            paragraph.AddFormattedText(label + ":").Color = Muted;
            paragraph.AddTab();
            paragraph.AddText(value);
        }
    }

    /// <summary>Adds a party: an optional label, the name in bold, then its lines.</summary>
    /// <param name="elements">Where the paragraphs go.</param>
    /// <param name="label">A heading above the name, or null for none.</param>
    /// <param name="party">The seller or the billed client.</param>
    /// <param name="firstSpaceBefore">Space above the first paragraph.</param>
    /// <returns>The first paragraph added, for spacing it from what comes before.</returns>
    private static Paragraph AddParty(DocumentElements elements, string? label, InvoicePdfParty party, double firstSpaceBefore = 0)
    {
        var first = label is null
            ? AddText(elements, party.Name, bold: true, EmphasisSize)
            : AddText(elements, label, bold: true, SmallSize, Muted);
        first.Format.SpaceBefore = firstSpaceBefore;
        if (label is not null)
        {
            AddText(elements, party.Name, bold: true, EmphasisSize);
        }

        foreach (var line in party.Lines)
        {
            AddText(elements, line);
        }

        return first;
    }

    /// <summary>Adds the items table, its header row repeated on every page it runs onto.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="lines">The lines printed, in their stored order.</param>
    /// <param name="money">How amounts are written.</param>
    /// <param name="labels">The column headings.</param>
    private static void AddItems(
        Section section, IReadOnlyList<InvoicePdfLine> lines, InvoicePdfMoneyFormat money, DocumentPdfLabels labels)
    {
        var table = section.AddTable();
        table.LeftPadding = 0;
        table.RightPadding = 0;
        table.TopPadding = RowPadding;
        table.BottomPadding = RowPadding;
        table.Borders.Bottom.Width = RuleWidth;
        table.Borders.Bottom.Color = Rule;
        table.AddColumn(ContentWidth - QtyWidth - (2 * MoneyWidth)).RightPadding = ColumnGap;
        table.AddColumn(QtyWidth);
        table.AddColumn(MoneyWidth);
        table.AddColumn(MoneyWidth);

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.TopPadding = TableGap;
        AddText(header.Cells[0].Elements, labels.Description, bold: true, color: Muted);
        AddText(header.Cells[1].Elements, labels.Quantity, bold: true, color: Muted, alignment: ParagraphAlignment.Right);
        AddText(header.Cells[2].Elements, labels.UnitPrice, bold: true, color: Muted, alignment: ParagraphAlignment.Right);
        AddText(header.Cells[3].Elements, labels.Amount, bold: true, color: Muted, alignment: ParagraphAlignment.Right);

        foreach (var line in lines)
        {
            var row = table.AddRow();
            AddText(row.Cells[0].Elements, line.Description);
            AddText(row.Cells[1].Elements, line.Quantity.ToString(CultureInfo.InvariantCulture), alignment: ParagraphAlignment.Right);
            AddText(row.Cells[2].Elements, FormatMoney(money, line.UnitPrice), alignment: ParagraphAlignment.Right);
            AddText(row.Cells[3].Elements, FormatMoney(money, line.Amount), alignment: ParagraphAlignment.Right);
        }
    }

    /// <summary>Adds subtotal, tax, credit when there is any, and the total, kept together on one page.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="money">How amounts are written.</param>
    /// <param name="subTotal">Sum of the lines before tax.</param>
    /// <param name="taxRate">Tax rate as a percentage.</param>
    /// <param name="tax">Tax amount.</param>
    /// <param name="credit">Credit applied; the line is left out when zero.</param>
    /// <param name="totalAmount">The total.</param>
    /// <param name="labels">The words of the totals.</param>
    /// <param name="totalLabel">The label of the total line.</param>
    private static void AddTotals(
        Section section,
        InvoicePdfMoneyFormat money,
        decimal subTotal,
        decimal taxRate,
        decimal tax,
        decimal credit,
        decimal totalAmount,
        DocumentPdfLabels labels,
        string totalLabel)
    {
        var rows = new List<(string Label, string Value)>
        {
            (labels.Subtotal, FormatMoney(money, subTotal)),
            ($"{labels.Tax} ({taxRate.ToString("0.##", CultureInfo.InvariantCulture)}%)", FormatMoney(money, tax)),
        };
        if (credit > 0)
        {
            rows.Add((labels.Credit, "-" + FormatMoney(money, credit)));
        }

        var table = section.AddTable();
        table.LeftPadding = 0;
        table.RightPadding = 0;
        table.TopPadding = 0;
        table.BottomPadding = 0;
        table.Rows.LeftIndent = ContentWidth - TotalsWidth;
        table.AddColumn(TotalsWidth - MoneyWidth);
        table.AddColumn(MoneyWidth);

        foreach (var (label, value) in rows)
        {
            var row = table.AddRow();
            AddText(row.Cells[0].Elements, label, color: Muted);
            AddText(row.Cells[1].Elements, value, alignment: ParagraphAlignment.Right);
        }

        table.Rows[0].TopPadding = TotalsGap;
        table.Rows[rows.Count - 1].BottomPadding = RuleGap;
        table.Rows[rows.Count - 1].Borders.Bottom.Width = RuleWidth;
        table.Rows[rows.Count - 1].Borders.Bottom.Color = Rule;

        var total = table.AddRow();
        total.TopPadding = RuleGap;
        AddText(total.Cells[0].Elements, totalLabel, bold: true, EmphasisSize);
        AddText(total.Cells[1].Elements, FormatMoney(money, totalAmount), bold: true, EmphasisSize, alignment: ParagraphAlignment.Right);

        table.Rows[0].KeepWith = rows.Count;
    }

    /// <summary>Adds "Page n of m", centred in the bottom margin of every page.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="labels">The footer words.</param>
    private static void AddFooter(Section section, DocumentPdfLabels labels)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = SmallSize;
        footer.Format.Font.Color = Muted;
        footer.Format.Font.Name = PdfFonts.Primary;
        footer.AddText($"{labels.Page} ");
        footer.AddPageField();
        footer.AddText($" {labels.Of} ");
        footer.AddNumPagesField();
    }

    /// <summary>The stamp an invoice carries: PAID (with the date), DRAFT, CANCELLED, PARTIALLY REFUNDED or REFUNDED; other statuses get none.</summary>
    /// <param name="model">The invoice.</param>
    /// <returns>The stamp text and colour, or a null text for no stamp.</returns>
    private static (string? Text, XColor Color) InvoiceStamp(InvoicePdfModel model) =>
        model.Status switch
        {
            InvoiceStatus.Paid => (
                model.PaidAt is { } paidAt ? $"PAID {paidAt.ToString(DateFormat, CultureInfo.InvariantCulture)}" : "PAID",
                PaidStamp),
            InvoiceStatus.Draft => ("DRAFT", DraftStamp),
            InvoiceStatus.Cancelled => ("CANCELLED", CancelledStamp),
            InvoiceStatus.PartiallyRefunded => ("PARTIALLY REFUNDED", RefundedStamp),
            InvoiceStatus.Refunded => ("REFUNDED", RefundedStamp),
            _ => (null, XColors.Transparent),
        };

    /// <summary>The stamp a credit note carries, in the language of its labels: a draft or a voided note is stamped, a standing one is not.</summary>
    /// <param name="model">The credit note.</param>
    /// <returns>The stamp text and colour, or a null text for no stamp.</returns>
    private static (string? Text, XColor Color) CreditNoteStamp(CreditNotePdfModel model) =>
        model.Status switch
        {
            CreditNoteStatus.Draft => (model.Labels.StampDraft, DraftStamp),
            CreditNoteStatus.Voided => (model.Labels.StampVoided, CancelledStamp),
            _ => (null, XColors.Transparent),
        };

    /// <summary>The status text of a credit note in the language of its labels.</summary>
    /// <param name="status">The note status.</param>
    /// <param name="labels">The credit note labels.</param>
    /// <returns>The words for the status.</returns>
    private static string CreditNoteStatusText(CreditNoteStatus status, CreditNotePdfLabels labels) =>
        status switch
        {
            CreditNoteStatus.Draft => labels.StatusDraft,
            CreditNoteStatus.Issued => labels.StatusIssued,
            CreditNoteStatus.Applied => labels.StatusApplied,
            _ => labels.StatusVoided,
        };

    /// <summary>Stamps the first page with a text across it; no text means no stamp.</summary>
    /// <param name="page">The rendered first page.</param>
    /// <param name="stamp">The stamp text and colour.</param>
    private static void DrawStamp(PdfSharp.Pdf.PdfPage page, (string? Text, XColor Color) stamp)
    {
        if (stamp.Text is null)
        {
            return;
        }

        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var centre = new XPoint(PageWidth / 2, StampCentreY);
        graphics.RotateAtTransform(StampAngle, centre);

        // The text goes through the same font fallback as every other string, so an Armenian or Russian stamp is never blank.
        graphics.DrawString(
            PdfFonts.ReplaceUncovered(stamp.Text, bold: true),
            new XFont(PdfFonts.Primary, StampSize, XFontStyleEx.Bold),
            new XSolidBrush(stamp.Color),
            centre,
            XStringFormats.Center);
    }

    /// <summary>Writes an amount in the invoice's currency.</summary>
    /// <remarks>
    /// Digits and grouping are invariant (<c>1,234.50</c>) because the labels are English; the
    /// prefix, suffix and decimal places are the currency's own.
    /// </remarks>
    /// <param name="money">How the invoice's currency is written.</param>
    /// <param name="amount">The amount; rounded to the currency's decimal places.</param>
    /// <returns>The prefix, the grouped amount and the suffix.</returns>
    private static string FormatMoney(InvoicePdfMoneyFormat money, decimal amount) =>
        money.Prefix
        + amount.ToString("N" + money.Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        + money.Suffix;

    /// <summary>Turns an enum name into words: <c>PaymentPending</c> becomes <c>Payment pending</c>.</summary>
    /// <param name="name">The enum member's name.</param>
    /// <returns>The words, first letter capitalised.</returns>
    private static string StatusText(string name) =>
        WordBoundary().Replace(name, m => " " + m.Value.ToLowerInvariant());

    /// <summary>An upper-case letter that starts a new word inside an enum name.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex("(?<=[a-z])[A-Z]")]
    private static partial Regex WordBoundary();

    /// <summary>
    /// Reads the logo when <paramref name="logoPath"/> names an image inside the branding upload
    /// directory; otherwise logs why at Debug and returns null.
    /// </summary>
    /// <param name="logoPath">The <c>portal.logo</c> value as stored.</param>
    /// <returns>The logo and the size to draw it at, or null to print without a logo.</returns>
    private PdfLogo? LoadLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;
        }

        if (Uri.TryCreate(logoPath, UriKind.Absolute, out var uri) && uri.Scheme is not "file")
        {
            _logger.LogDebug("Invoice PDF logo skipped: {LogoPath} is not a local upload.", logoPath);
            return null;
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var baseDir = Path.GetFullPath(Path.Combine(webRoot, _branding.RelativePath)) + Path.DirectorySeparatorChar;
        var relative = logoPath.Split('?', 2)[0].TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(webRoot, relative));
        if (!full.StartsWith(baseDir, StringComparison.Ordinal) || !File.Exists(full))
        {
            _logger.LogDebug("Invoice PDF logo skipped: {LogoPath} is not a file in the branding directory.", logoPath);
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(full);
            using var image = XImage.FromStream(new MemoryStream(bytes));
            var scale = Math.Min(1, Math.Min(LogoMaxWidth / image.PixelWidth, LogoMaxHeight / image.PixelHeight));
            return new PdfLogo(bytes, image.PixelWidth * scale, image.PixelHeight * scale);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Invoice PDF logo skipped: {LogoPath} could not be read or decoded.", logoPath);
            return null;
        }
    }
}
