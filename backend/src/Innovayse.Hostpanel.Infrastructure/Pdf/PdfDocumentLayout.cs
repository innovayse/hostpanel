using System.Globalization;
using System.Text;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// The one A4 layout of an invoice and a credit note, drawn with MigraDoc and written with PDFsharp.
/// </summary>
/// <remarks>
/// <para>
/// MigraDoc does the layout: wrapping, the items table with its header row repeated on every page it runs onto, page breaks
/// and the footer. The one thing drawn afterwards, straight onto the first page with PDFsharp, is the rounded background of
/// the status pill, because MigraDoc cannot round a corner; it is placed from the layout MigraDoc reports for the title.
/// Text is set as text, so the PDF stays searchable and selectable.
/// </para>
/// <para>
/// A document is a column of blocks with generous white space between them: logo, title, meta, the two parties side by side, the
/// headline, the items, the totals stacked on the right, the bank details, the notes and a closing line.
/// </para>
/// </remarks>
internal static class PdfDocumentLayout
{
    /// <summary>A4 width in points.</summary>
    private const double PageWidth = 595;

    /// <summary>A4 height in points.</summary>
    private const double PageHeight = 842;

    /// <summary>Margin on the top and sides, in points.</summary>
    private const double Margin = 48;

    /// <summary>Space kept above the bottom margin for the footer.</summary>
    private const double FooterHeight = 28;

    /// <summary>Size of body text.</summary>
    private const double BodySize = 9;

    /// <summary>Size of labels, headings and the footer.</summary>
    private const double SmallSize = 8;

    /// <summary>Size of the strong totals row and the link under the headline.</summary>
    private const double EmphasisSize = 11;

    /// <summary>Size of the headline above the table.</summary>
    private const double HeadlineSize = 18;

    /// <summary>Size of the title.</summary>
    private const double TitleSize = 24;

    /// <summary>Thickness of rules.</summary>
    private const double RuleWidth = 0.75;

    /// <summary>Space above and below the text of a table row.</summary>
    private const double RowPadding = 5;

    /// <summary>Space between the blocks of the first page.</summary>
    private const double BlockGap = 28;

    /// <summary>Space between a heading and what it heads.</summary>
    private const double TitleGap = 14;

    /// <summary>Gap kept between columns.</summary>
    private const double ColumnGap = 10;

    /// <summary>Width of the page between the margins.</summary>
    private const double ContentWidth = PageWidth - (2 * Margin);

    /// <summary>Width of the Qty column.</summary>
    private const double QtyWidth = 44;

    /// <summary>Width of the unit price column.</summary>
    private const double UnitPriceWidth = 84;

    /// <summary>Width of the tax column.</summary>
    private const double TaxWidth = 44;

    /// <summary>Width of the amount column.</summary>
    private const double AmountWidth = 90;

    /// <summary>
    /// Width of the description column, the narrowest that customer-supplied text goes into. A word wider than this cannot
    /// fit on a line anywhere it is printed.
    /// </summary>
    private const double DescriptionWidth = ContentWidth - QtyWidth - UnitPriceWidth - TaxWidth - AmountWidth - ColumnGap;

    /// <summary>Shortest word worth measuring: nothing shorter is near <see cref="DescriptionWidth"/>.</summary>
    private const int LongWordLength = 20;

    /// <summary>A break opportunity that prints nothing; MigraDoc breaks a line there when it must.</summary>
    private const char ZeroWidthSpace = '​';

    /// <summary>Width of the totals block on the right.</summary>
    private const double TotalsWidth = 250;

    /// <summary>Width of the value column of the totals.</summary>
    private const double TotalsValueWidth = 120;

    /// <summary>Width of the label column of the meta block and the bank details.</summary>
    private const double LabelWidth = 130;

    /// <summary>Height of the status pill.</summary>
    private const double PillHeight = 17;

    /// <summary>Space left and right of the text inside the pill.</summary>
    private const double PillPadding = 9;

    /// <summary>Space between the title and the pill.</summary>
    private const double PillGap = 12;

    /// <summary>Size of the pill's text.</summary>
    private const double PillTextSize = 8;

    /// <summary>Text colour.</summary>
    private static readonly Color Ink = new(0x1F, 0x29, 0x37);

    /// <summary>Colour of labels and secondary text.</summary>
    private static readonly Color Muted = new(0x6B, 0x72, 0x80);

    /// <summary>Colour of rules.</summary>
    private static readonly Color Rule = new(0xE5, 0xE7, 0xEB);

    /// <summary>Background of the bank details box.</summary>
    private static readonly Color Panel = new(0xF9, 0xFA, 0xFB);

    /// <summary>The accent when the operator has set no brand colour: a neutral slate.</summary>
    private static readonly Color NeutralAccent = new(0x37, 0x41, 0x51);

    /// <summary>Lays out and renders a document.</summary>
    /// <param name="content">Everything the document prints, every text formatted.</param>
    /// <param name="logo">The logo and the size to draw it at, or null for none.</param>
    /// <returns>The PDF file's bytes.</returns>
    public static byte[] Render(PdfDocumentContent content, PdfLogo? logo)
    {
        PdfFonts.EnsureInitialized();

        var document = new Document();
        document.Info.Title = content.Name;
        document.Info.Author = content.Seller.Name;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.Primary;
        normal.Font.Size = BodySize;
        normal.Font.Color = Ink;

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageWidth = PageWidth;
        setup.PageHeight = PageHeight;
        setup.TopMargin = Margin;
        setup.LeftMargin = Margin;
        setup.RightMargin = Margin;
        setup.BottomMargin = Margin + FooterHeight;
        setup.FooterDistance = Margin - SmallSize;

        var accent = content.AccentColour is { } hex && BrandPalette.Parse(hex) is { } rgb
            ? new Color((byte)rgb.R, (byte)rgb.G, (byte)rgb.B)
            : NeutralAccent;

        AddLogo(section, logo);
        var titleTable = AddTitle(section, content.Title);
        AddMeta(section, content.Meta);
        AddParties(section, content);
        AddHeadline(section, content.Headline, accent);
        AddGap(section, BlockGap);
        AddItems(section, content, accent);
        AddTotals(section, content.Totals, accent);
        AddDetails(section, content.Details, accent);
        AddNotes(section, content.Notes);
        AddClosing(section, content.Closing);
        AddFooter(section, content);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var pdf = renderer.PdfDocument;
        var titleArea = renderer.DocumentRenderer.GetRenderInfoFromPage(1)
            .First(info => ReferenceEquals(info.DocumentObject, titleTable))
            .LayoutInfo.ContentArea;
        DrawPill(pdf.Pages[0], content.Pill, content.Title, titleArea.X.Point, titleArea.Y.Point, titleArea.Height.Point);

        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    /// <summary>Adds a paragraph of text, setting any character the main font lacks in the fallback font.</summary>
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
        AppendRuns(paragraph, text, bold, size);
        return paragraph;
    }

    /// <summary>
    /// Appends text to a paragraph, setting any character the main font lacks in the fallback font and printing a placeholder
    /// for any character neither has.
    /// </summary>
    /// <param name="paragraph">The paragraph to append to.</param>
    /// <param name="text">The text to set.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    /// <param name="size">Font size in points.</param>
    private static void AppendRuns(Paragraph paragraph, string text, bool bold, double size)
    {
        var printable = BreakLongWords(PdfFonts.ReplaceUncovered(text, bold), bold, size);
        foreach (var (run, useFallback) in PdfFonts.Runs(printable, bold))
        {
            var formatted = paragraph.AddFormattedText(run);
            if (useFallback)
            {
                formatted.Font.Name = PdfFonts.Fallback;
            }
        }
    }

    /// <summary>
    /// Lets MigraDoc break a word too wide for any column: it only breaks lines at spaces, so a long unbroken word (a hostname,
    /// a pasted reference) would otherwise run off the page.
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

    /// <summary>Adds white space of an exact height between two blocks.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="height">The height in points.</param>
    private static void AddGap(Section section, double height)
    {
        var gap = section.AddParagraph();
        gap.Format.LineSpacingRule = LineSpacingRule.Exactly;
        gap.Format.LineSpacing = height;
    }

    /// <summary>Starts a borderless table with no padding at the sides, so its text lines up with the margin.</summary>
    /// <param name="section">The section being built.</param>
    /// <returns>The empty table.</returns>
    private static Table NewTable(Section section)
    {
        var table = section.AddTable();
        table.LeftPadding = 0;
        table.RightPadding = 0;
        table.TopPadding = 0;
        table.BottomPadding = 0;
        return table;
    }

    /// <summary>Adds the small logo, top left.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="logo">The logo and the size to draw it at, or null for none.</param>
    private static void AddLogo(Section section, PdfLogo? logo)
    {
        if (logo is null)
        {
            return;
        }

        var paragraph = section.AddParagraph();
        var picture = paragraph.AddImage("base64:" + Convert.ToBase64String(logo.Bytes));
        picture.LockAspectRatio = false;
        picture.Width = logo.Width;
        picture.Height = logo.Height;
        AddGap(section, TitleGap);
    }

    /// <summary>Adds the title, in a one-cell table whose position the status pill is drawn from.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="title">The name of the document.</param>
    /// <returns>The table, to find where it was laid out.</returns>
    private static Table AddTitle(Section section, string title)
    {
        var table = NewTable(section);
        table.AddColumn(ContentWidth);
        var row = table.AddRow();
        AddText(row.Cells[0].Elements, title, bold: true, TitleSize);
        return table;
    }

    /// <summary>Adds the compact meta block: a muted label and its value per row.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="meta">The rows.</param>
    private static void AddMeta(Section section, IReadOnlyList<PdfRow> meta)
    {
        var table = NewTable(section);
        table.AddColumn(LabelWidth);
        table.AddColumn(ContentWidth - LabelWidth);
        foreach (var item in meta)
        {
            var row = table.AddRow();
            row.TopPadding = 2;
            row.BottomPadding = 2;
            AddText(row.Cells[0].Elements, item.Label, color: Muted);
            AddText(row.Cells[1].Elements, item.Value);
        }

        table.Rows[0].TopPadding = TitleGap;
    }

    /// <summary>Adds the seller and the client side by side.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="content">The document.</param>
    private static void AddParties(Section section, PdfDocumentContent content)
    {
        var table = NewTable(section);
        var half = (ContentWidth - ColumnGap) / 2;
        table.AddColumn(half).RightPadding = ColumnGap;
        table.AddColumn(half);
        var row = table.AddRow();
        row.TopPadding = BlockGap;
        AddParty(row.Cells[0].Elements, content.Labels.From, content.Seller);
        AddParty(row.Cells[1].Elements, content.Labels.BillTo, content.BillTo);
    }

    /// <summary>Adds a party: a muted heading, the name in bold, then its lines.</summary>
    /// <param name="elements">Where the paragraphs go.</param>
    /// <param name="heading">The heading above the name.</param>
    /// <param name="party">The seller or the billed client.</param>
    private static void AddParty(DocumentElements elements, string heading, InvoicePdfParty party)
    {
        AddText(elements, heading, bold: true, SmallSize, Muted).Format.SpaceAfter = 4;
        AddText(elements, party.Name, bold: true);
        foreach (var line in party.Lines)
        {
            AddText(elements, line, color: Muted);
        }
    }

    /// <summary>Adds the large headline above the table, with its link under it.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="headline">The headline, or null for none.</param>
    /// <param name="accent">The colour of the link.</param>
    private static void AddHeadline(Section section, PdfHeadline? headline, Color accent)
    {
        if (headline is null)
        {
            return;
        }

        AddGap(section, BlockGap);
        AddText(section.Elements, headline.Text, bold: true, HeadlineSize);
        if (headline.LinkText is null || headline.LinkUrl is null)
        {
            return;
        }

        var paragraph = section.AddParagraph();
        paragraph.Format.SpaceBefore = 6;
        var link = paragraph.AddHyperlink(headline.LinkUrl, HyperlinkType.Web);
        var text = link.AddFormattedText(PdfFonts.ReplaceUncovered(headline.LinkText, bold: true));
        text.Font.Bold = true;
        text.Font.Size = EmphasisSize;
        text.Font.Color = accent;
        text.Font.Underline = Underline.Single;
    }

    /// <summary>Adds the items table, its header row and the thin rule under it repeated on every page it runs onto.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="content">The document.</param>
    /// <param name="accent">The colour of the rule under the header.</param>
    private static void AddItems(Section section, PdfDocumentContent content, Color accent)
    {
        var labels = content.Labels;
        var table = NewTable(section);
        table.TopPadding = RowPadding;
        table.BottomPadding = RowPadding;
        table.AddColumn(DescriptionWidth + ColumnGap).RightPadding = ColumnGap;
        table.AddColumn(QtyWidth);
        table.AddColumn(UnitPriceWidth);
        table.AddColumn(TaxWidth);
        table.AddColumn(AmountWidth);

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Borders.Bottom.Width = RuleWidth;
        header.Borders.Bottom.Color = accent;
        AddText(header.Cells[0].Elements, labels.Description, bold: true, SmallSize, Muted);
        AddText(header.Cells[1].Elements, labels.Quantity, bold: true, SmallSize, Muted, ParagraphAlignment.Right);
        AddText(header.Cells[2].Elements, labels.UnitPrice, bold: true, SmallSize, Muted, ParagraphAlignment.Right);
        AddText(header.Cells[3].Elements, labels.Tax, bold: true, SmallSize, Muted, ParagraphAlignment.Right);
        AddText(header.Cells[4].Elements, labels.Amount, bold: true, SmallSize, Muted, ParagraphAlignment.Right);

        foreach (var item in content.Items)
        {
            var row = table.AddRow();
            AddText(row.Cells[0].Elements, item.Description);
            AddText(row.Cells[1].Elements, item.Quantity, color: Muted, alignment: ParagraphAlignment.Right);
            AddText(row.Cells[2].Elements, item.UnitPrice, color: Muted, alignment: ParagraphAlignment.Right);
            AddText(row.Cells[3].Elements, item.Tax, color: Muted, alignment: ParagraphAlignment.Right);
            AddText(row.Cells[4].Elements, item.Amount, alignment: ParagraphAlignment.Right);
        }
    }

    /// <summary>Adds the totals stacked on the right, kept together on one page.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="totals">The rows, the strong one set large and bold.</param>
    /// <param name="accent">The colour of the strong row's value.</param>
    private static void AddTotals(Section section, IReadOnlyList<PdfRow> totals, Color accent)
    {
        AddGap(section, TitleGap);
        var table = NewTable(section);
        table.Rows.LeftIndent = ContentWidth - TotalsWidth;
        table.AddColumn(TotalsWidth - TotalsValueWidth);
        table.AddColumn(TotalsValueWidth);

        foreach (var item in totals)
        {
            var row = table.AddRow();
            row.TopPadding = item.RuleAbove ? RowPadding + 2 : 2;
            row.BottomPadding = item.Strong ? RowPadding : 2;
            if (item.RuleAbove)
            {
                row.Borders.Top.Width = RuleWidth;
                row.Borders.Top.Color = Rule;
            }

            var size = item.Strong ? EmphasisSize : BodySize;
            AddText(row.Cells[0].Elements, item.Label, item.Strong, size, item.Strong ? Ink : Muted);
            AddText(row.Cells[1].Elements, item.Value, item.Strong, size, item.Strong ? accent : Ink, ParagraphAlignment.Right);
        }

        table.Rows[0].KeepWith = totals.Count - 1;
    }

    /// <summary>Adds the bank details in a quiet shaded box, kept together on one page.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="details">The box, or null for none.</param>
    /// <param name="accent">The colour of the heading.</param>
    private static void AddDetails(Section section, PdfDetails? details, Color accent)
    {
        if (details is null)
        {
            return;
        }

        AddGap(section, BlockGap);
        var table = NewTable(section);
        table.LeftPadding = TitleGap;
        table.RightPadding = TitleGap;
        table.Shading.Color = Panel;
        table.AddColumn(LabelWidth);
        table.AddColumn(ContentWidth - LabelWidth);

        var heading = table.AddRow();
        heading.TopPadding = TitleGap;
        heading.BottomPadding = 6;
        heading.Cells[0].MergeRight = 1;
        AddText(heading.Cells[0].Elements, details.Heading, bold: true, color: accent);
        foreach (var item in details.Rows)
        {
            var row = table.AddRow();
            row.TopPadding = 3;
            row.BottomPadding = 3;
            AddText(row.Cells[0].Elements, item.Label, color: Muted);
            AddText(row.Cells[1].Elements, item.Value);
        }

        table.Rows[table.Rows.Count - 1].BottomPadding = TitleGap;
        table.Rows[0].KeepWith = table.Rows.Count - 1;
    }

    /// <summary>Adds the paragraphs under the totals.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="notes">The paragraphs.</param>
    private static void AddNotes(Section section, IReadOnlyList<PdfNote> notes)
    {
        foreach (var note in notes)
        {
            AddGap(section, BlockGap);
            AddText(section.Elements, note.Heading, bold: true, SmallSize, Muted).Format.KeepWithNext = true;
            AddText(section.Elements, note.Text).Format.SpaceBefore = 3;
        }
    }

    /// <summary>Adds the quiet closing line.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="closing">The line.</param>
    private static void AddClosing(Section section, string closing)
    {
        AddGap(section, BlockGap);
        AddText(section.Elements, closing, size: SmallSize, color: Muted);
    }

    /// <summary>Adds the footer of every page: the document's number and amount on the left, the page number on the right.</summary>
    /// <param name="section">The section being built.</param>
    /// <param name="content">The document.</param>
    private static void AddFooter(Section section, PdfDocumentContent content)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = SmallSize;
        footer.Format.Font.Color = Muted;
        footer.Format.Font.Name = PdfFonts.Primary;
        footer.Format.TabStops.AddTabStop(ContentWidth, TabAlignment.Right);
        footer.Format.Borders.Top.Width = RuleWidth;
        footer.Format.Borders.Top.Color = Rule;
        footer.Format.Borders.DistanceFromTop = 8;
        AppendRuns(footer, content.Footer, bold: false, SmallSize);
        footer.AddTab();
        footer.AddText($"{content.Labels.Page} ");
        footer.AddPageField();
        footer.AddText($" {content.Labels.Of} ");
        footer.AddNumPagesField();
    }

    /// <summary>Draws the status pill beside the title on the first page.</summary>
    /// <param name="page">The rendered first page.</param>
    /// <param name="pill">The status words and colours.</param>
    /// <param name="title">The title text, whose width the pill follows.</param>
    /// <param name="x">Left of the title's table, in points.</param>
    /// <param name="y">Top of the title's table, in points.</param>
    /// <param name="height">Height of the title's table, in points.</param>
    private static void DrawPill(PdfSharp.Pdf.PdfPage page, PdfPill pill, string title, double x, double y, double height)
    {
        var text = PdfFonts.ReplaceUncovered(pill.Text, bold: true);
        var titleFont = new XFont(PdfFonts.Primary, TitleSize, XFontStyleEx.Bold);
        var pillFont = new XFont(PdfFonts.Primary, PillTextSize, XFontStyleEx.Bold);
        using var measure = XGraphics.CreateMeasureContext(new XSize(PageWidth, PageHeight), XGraphicsUnit.Point, XPageDirection.Downwards);
        var titleWidth = measure.MeasureString(PdfFonts.ReplaceUncovered(title, bold: true), titleFont).Width;
        var width = measure.MeasureString(text, pillFont).Width + (2 * PillPadding);
        var area = new XRect(x + titleWidth + PillGap, y + ((height - PillHeight) / 2), width, PillHeight);

        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        graphics.DrawRoundedRectangle(new XSolidBrush(pill.Fill), area, new XSize(PillHeight, PillHeight));
        graphics.DrawString(text, pillFont, new XSolidBrush(pill.Ink), area, XStringFormats.Center);
    }
}
