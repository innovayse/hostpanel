using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Pdf;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Pdf;

/// <summary>
/// Tests for the credit note side of the PDF renderer: the document prints its words in English, Russian and Armenian, the
/// Armenian text is really drawn with a font that has the letters (and the dram sign), a voided note is stamped in its language,
/// and a credit note shares the render limit with invoices.
/// </summary>
public sealed class MigraDocCreditNotePdfRendererTests : IDisposable
{
    /// <summary>A web root of the test's own, so nothing outside it is ever read.</summary>
    private readonly string _webRoot = Directory.CreateTempSubdirectory("credit-note-pdf-").FullName;

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    /// <summary>Builds a renderer whose web root is the test's own directory.</summary>
    /// <param name="gate">The render gate; a fresh one with the default limit when null.</param>
    /// <returns>The renderer under test.</returns>
    private MigraDocInvoicePdfRenderer Create(InvoicePdfRenderGate? gate = null) =>
        new(
            new StubWebHostEnvironment(Path.Combine(_webRoot, "web")),
            Options.Create(new BrandingOptions()),
            gate ?? new InvoicePdfRenderGate(Options.Create(new InvoicePdfOptions())),
            NullLogger<MigraDocInvoicePdfRenderer>.Instance);

    /// <summary>A one-line credit note with Cyrillic and Armenian names on it, in the given language and state.</summary>
    /// <param name="language">The language of the labels.</param>
    /// <param name="status">The state of the note.</param>
    /// <returns>The model.</returns>
    private static CreditNotePdfModel Sample(string language, CreditNoteStatus status = CreditNoteStatus.Applied) => new(
        Number: "CN-0007",
        Status: status,
        IssuedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        InvoiceId: 42,
        Reason: "Ծառայությունը չի աշխատել",
        Seller: new InvoicePdfParty("Fairhaven Hosting", ["1 Market Street", "Tax ID: 123456"]),
        BillTo: new InvoicePdfParty("Арам Петросян", ["Ծաղկաձոր, Հայաստան"]),
        LogoPath: null,
        Lines: [new InvoicePdfLine("Shared hosting", 1, 12.50m, 12.50m)],
        SubTotal: 12.50m,
        TaxRate: 20m,
        Tax: 2.50m,
        Total: 15.00m,
        Money: new InvoicePdfMoneyFormat(string.Empty, " ֏", 0),
        Labels: CreditNotePdfLabelSets.For(language));

    /// <summary>Renders a note and reads back its text without whitespace.</summary>
    /// <param name="model">The note to render.</param>
    /// <returns>All the text of every page, whitespace removed.</returns>
    private async Task<string> TextAsync(CreditNotePdfModel model)
    {
        var bytes = (await Create().RenderCreditNoteAsync(model, CancellationToken.None)).Value!;
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        using var document = PdfDocument.Open(bytes);
        return string.Concat(document.GetPages().SelectMany(p => p.Text).Where(c => !char.IsWhiteSpace(c)));
    }

    /// <summary>Each language prints its own title, labels and totals, with the number and the amounts.</summary>
    /// <param name="language">The language under test.</param>
    /// <param name="title">The title expected.</param>
    /// <param name="totalLabel">The total label expected.</param>
    [Theory]
    [InlineData("en", "Creditnote", "Totalcredited")]
    [InlineData("ru", "Кредит-нота", "Итогокзачёту")]
    [InlineData("hy", "Կրեդիտ-նոտա", "Կրեդիտիընդհանուրգումար")]
    public async Task Render_PrintsTheLabelsInTheLanguage(string language, string title, string totalLabel)
    {
        var text = await TextAsync(Sample(language));

        text.Should().Contain(title + "CN-0007").And.Contain(totalLabel).And.Contain("#42");
        text.Should().Contain("Fairhaven").And.Contain("2026-10-01");
        text.Should().NotContain("\0");
    }

    /// <summary>An unknown or missing language falls back to English so no label is ever blank.</summary>
    /// <param name="language">A language the panel has no set for, or none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    public async Task Render_UnknownLanguage_FallsBackToEnglish(string? language)
    {
        var text = await TextAsync(Sample(language ?? string.Empty));

        text.Should().Contain("CreditnoteCN-0007").And.Contain("Totalcredited");
    }

    /// <summary>
    /// Armenian text prints as real glyphs: the reason, the client address and the dram sign after the amounts come back from the
    /// PDF as the same characters, and no missing-glyph box (read back as U+0000) is drawn.
    /// </summary>
    [Fact]
    public async Task Render_DrawsArmenianLettersAndTheDramSign()
    {
        var text = await TextAsync(Sample("hy"));

        text.Should().Contain("Ծառայությունըչիաշխատել").And.Contain("Ծաղկաձոր,Հայաստան").And.Contain("15֏");
        text.Should().NotContain("\0").And.NotContain("�");
    }

    /// <summary>A voided note is stamped VOIDED in its language, and a standing note carries no stamp.</summary>
    /// <param name="language">The language under test.</param>
    /// <param name="stamp">The stamp expected on a voided note.</param>
    [Theory]
    [InlineData("en", "VOIDED")]
    [InlineData("ru", "АННУЛИРОВАНА")]
    [InlineData("hy", "ՉԵՂԱՐԿՎԱԾ")]
    public async Task Render_StampsAVoidedNoteInItsLanguage(string language, string stamp)
    {
        var voided = await TextAsync(Sample(language, CreditNoteStatus.Voided));
        var standing = await TextAsync(Sample(language));

        voided.Should().Contain(stamp);
        standing.Should().NotContain(stamp);
    }

    /// <summary>A draft is stamped so nobody mistakes it for an issued note.</summary>
    [Fact]
    public async Task Render_StampsADraft()
    {
        var text = await TextAsync(Sample("en", CreditNoteStatus.Draft));

        text.Should().Contain("DRAFT");
    }

    /// <summary>A credit note takes a slot of the same gate as an invoice: with the only slot held, it is refused as busy.</summary>
    [Fact]
    public async Task Render_SharesTheRenderLimitWithInvoices()
    {
        using var gate = new InvoicePdfRenderGate(Options.Create(new InvoicePdfOptions { MaxConcurrentRenders = 1, MaxRenderWaitSeconds = 1 }));
        using var held = await gate.TryEnterAsync(CancellationToken.None);

        var result = await Create(gate).RenderCreditNoteAsync(Sample("en"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVOICE_PDF_RENDER_BUSY");
    }

    /// <summary>The label sets cover the same words in every language: none is blank.</summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void LabelSets_HaveNoBlankLabel(string language)
    {
        var labels = CreditNotePdfLabelSets.For(language);

        var all = typeof(CreditNotePdfLabels).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => (string)p.GetValue(labels)!)
            .Concat(typeof(DocumentPdfLabels).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => (string)p.GetValue(labels.Document)!));
        all.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s));
    }
}
