using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Pdf;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Pdf;

/// <summary>
/// Tests for the invoice PDF renderer.
/// </summary>
/// <remarks>
/// The output is read back with PdfPig, so the assertions are about what a reader of the PDF sees
/// (the text, the pages, the images) rather than about how the bytes are laid out.
/// </remarks>
public sealed class MigraDocInvoicePdfRendererTests : IDisposable
{
    /// <summary>A web root of the test's own, so nothing outside it is ever read.</summary>
    private readonly string _webRoot = Directory.CreateTempSubdirectory("invoice-pdf-").FullName;

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    /// <summary>
    /// Builds a renderer whose web root is <c>web</c> inside this test's temporary directory, so a
    /// path that climbs out of the web root still lands somewhere the test owns.
    /// </summary>
    /// <param name="gate">The render gate; a fresh one with the default limit when null.</param>
    /// <returns>The renderer under test.</returns>
    private MigraDocInvoicePdfRenderer Create(InvoicePdfRenderGate? gate = null) =>
        new(
            new StubWebHostEnvironment(Path.Combine(_webRoot, "web")),
            Options.Create(new BrandingOptions()),
            gate ?? Gate(new InvoicePdfOptions().MaxConcurrentRenders),
            NullLogger<MigraDocInvoicePdfRenderer>.Instance);

    /// <summary>A render gate that lets <paramref name="limit"/> renders run at once.</summary>
    /// <param name="limit">The most renders in flight.</param>
    /// <returns>The gate; the test disposes it.</returns>
    private static InvoicePdfRenderGate Gate(int limit) =>
        new(Options.Create(new InvoicePdfOptions { MaxConcurrentRenders = limit }));

    /// <summary>A one-line unpaid invoice with Cyrillic and Armenian names on it.</summary>
    /// <returns>The model.</returns>
    private static InvoicePdfModel SampleModel() => new(
        InvoiceId: 42,
        Status: InvoiceStatus.Unpaid,
        InvoiceDate: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        DueDate: new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero),
        PaidAt: null,
        Seller: new InvoicePdfParty("Fairhaven Hosting", ["1 Market Street", "Tax ID: 123456"]),
        BillTo: new InvoicePdfParty("Арам Петросян", ["Ծաղկաձոր, Հայաստան"]),
        LogoPath: null,
        Lines: [new InvoicePdfLine("Shared hosting — Starter", 1, 12.50m, 12.50m)],
        SubTotal: 12.50m,
        TaxRate: 20m,
        Tax: 2.50m,
        Credit: 0m,
        Total: 15.00m,
        Money: new InvoicePdfMoneyFormat("$", string.Empty, 2));

    /// <summary>Renders a model and opens the result for reading.</summary>
    /// <param name="model">The invoice to render.</param>
    /// <returns>The parsed document; the caller disposes it.</returns>
    private async Task<PdfDocument> RenderAsync(InvoicePdfModel model)
    {
        var bytes = await Create().RenderAsync(model, CancellationToken.None);
        return PdfDocument.Open(bytes);
    }

    /// <summary>All the text in the document with whitespace removed, so wrapping cannot break a match.</summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The text of every page, concatenated, without whitespace.</returns>
    private static string CompactText(PdfDocument document) =>
        Compact(string.Concat(document.GetPages().Select(p => p.Text)));

    /// <summary>Removes every whitespace character.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The text without whitespace.</returns>
    private static string Compact(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

    /// <summary>
    /// With the limit at one and its slot taken, a render waits rather than starting, and goes ahead
    /// as soon as the slot is given back.
    /// </summary>
    [Fact]
    public async Task Render_WaitsForAFreeSlot()
    {
        using var gate = Gate(1);
        var renderer = Create(gate);
        var held = await gate.EnterAsync(CancellationToken.None);

        var render = renderer.RenderAsync(SampleModel(), CancellationToken.None);

        render.IsCompleted.Should().BeFalse();
        held.Dispose();
        var bytes = await render;
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    /// <summary>A render cancelled while it waits gives up without ever taking the slot.</summary>
    [Fact]
    public async Task Render_CancelledWhileWaiting_NeverTakesTheSlot()
    {
        using var gate = Gate(1);
        var renderer = Create(gate);
        var held = await gate.EnterAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var render = renderer.RenderAsync(SampleModel(), cts.Token);
        await cts.CancelAsync();

        await render.Invoking(r => r).Should().ThrowAsync<OperationCanceledException>();
        held.Dispose();
        var next = gate.EnterAsync(CancellationToken.None);
        next.IsCompletedSuccessfully.Should().BeTrue("the cancelled render must not be holding the only slot");
        (await next).Dispose();
    }

    /// <summary>A render that fails gives its slot back, so the next one is not stuck behind it.</summary>
    [Fact]
    public async Task Render_ThatFails_ReleasesTheSlot()
    {
        using var gate = Gate(1);
        var renderer = Create(gate);

        await renderer.Invoking(r => r.RenderAsync(SampleModel() with { Lines = null! }, CancellationToken.None))
            .Should().ThrowAsync<Exception>();

        var next = renderer.RenderAsync(SampleModel(), CancellationToken.None);
        next.IsCompleted.Should().BeTrue("the failed render must have released the only slot");
        (await next).Should().NotBeEmpty();
    }

    /// <summary>
    /// A render still finishing when the gate is disposed at shutdown gives its slot back without
    /// throwing, so the shutdown cannot replace the render's own outcome with an exception.
    /// </summary>
    [Fact]
    public async Task Gate_DisposedWhileASlotIsHeld_ReleasesQuietly()
    {
        var gate = Gate(1);
        var held = await gate.EnterAsync(CancellationToken.None);

        gate.Dispose();

        held.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    /// <summary>The output is a PDF of one page for a one-line invoice.</summary>
    [Fact]
    public async Task Render_ProducesPdf()
    {
        var bytes = await Create().RenderAsync(SampleModel(), CancellationToken.None);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        using var document = PdfDocument.Open(bytes);
        document.NumberOfPages.Should().Be(1);
    }

    /// <summary>Cyrillic and Armenian text comes out as written.</summary>
    [Fact]
    public async Task Render_PrintsNonLatinText()
    {
        using var document = await RenderAsync(SampleModel());

        var text = CompactText(document);
        text.Should().Contain(Compact("Арам Петросян"));
        text.Should().Contain(Compact("Ծաղկաձոր, Հայաստան"));
    }

    /// <summary>
    /// Client names are not limited to the panel's own languages: Georgian, Greek, Vietnamese and
    /// the Ukrainian apostrophe come out as written rather than as blanks.
    /// </summary>
    [Fact]
    public async Task Render_PrintsNamesBeyondThePanelLanguages()
    {
        string[] names = ["Αλέξης Παπαδόπουλος", "Nguyễn Văn An", "Марʼяна Коваль"];
        var model = SampleModel() with { BillTo = new InvoicePdfParty("გიორგი ბერიძე", names) };

        using var document = await RenderAsync(model);

        var text = CompactText(document);
        text.Should().Contain(Compact("გიორგი ბერიძე"));
        foreach (var name in names)
        {
            text.Should().Contain(Compact(name));
        }
    }

    /// <summary>The header, the seller, the totals and the dates are all on the page.</summary>
    [Fact]
    public async Task Render_PrintsHeaderTotalsAndDates()
    {
        using var document = await RenderAsync(SampleModel());

        var text = CompactText(document);
        text.Should().Contain("Invoice#42");
        text.Should().Contain("FairhavenHosting");
        text.Should().Contain("TaxID:123456");
        text.Should().Contain("$15.00");
        text.Should().Contain("Tax(20%)");
        text.Should().Contain("2026-10-01");
        text.Should().Contain("2026-10-15");
    }

    /// <summary>
    /// A long one-line seller address (the admin field is a single input) wraps inside the seller
    /// column instead of printing over the invoice dates on the right.
    /// </summary>
    [Fact]
    public async Task Render_WrapsLongSellerAddressClearOfTheDates()
    {
        const string address = "Building 12, Office 405, Northern Avenue Business Centre, Kentron District, Yerevan 0001, Republic of Armenia";
        var model = SampleModel() with
        {
            Seller = new InvoicePdfParty("Fairhaven Hosting and Domain Services Limited Liability Company", [address]),
        };

        using var document = await RenderAsync(model);

        var page = document.GetPage(1);
        var words = page.GetWords().ToList();

        // The right-hand header block: "Invoice #42", "Invoice date:", "Due date:", "Status:".
        var datesLeft = words
            .Where(w => w.Text is "Invoice" or "Due" or "Status:" && w.BoundingBox.Left > page.Width / 2)
            .Min(w => w.BoundingBox.Left);
        var addressWords = address.Split(' ').Concat("Fairhaven Hosting and Domain Services Limited Liability Company".Split(' ')).ToHashSet();
        var printed = words.Where(w => addressWords.Contains(w.Text) && w.BoundingBox.Left < datesLeft).ToList();
        printed.Should().NotBeEmpty();
        printed.Should().OnlyContain(w => w.BoundingBox.Right < datesLeft);
        words.Select(w => w.Text).Should().Contain(address.Split(' '), "every word of the address is still printed");
    }

    /// <summary>A long bill-to line wraps inside the right margin instead of running off the page.</summary>
    [Fact]
    public async Task Render_WrapsLongBillToLineInsideThePage()
    {
        var street = string.Join(' ', Enumerable.Repeat("Abovyan", 30));
        var model = SampleModel() with { BillTo = new InvoicePdfParty("Արամ Պետրոսյան", [street]) };

        using var document = await RenderAsync(model);

        var page = document.GetPage(1);
        var pieces = page.GetWords().Where(w => w.Text == "Abovyan").ToList();
        pieces.Should().HaveCount(30);
        pieces.Should().OnlyContain(w => w.BoundingBox.Right <= page.Width - 40);
    }

    /// <summary>A credit line appears only when credit was applied.</summary>
    [Fact]
    public async Task Render_OmitsCreditLineWhenZero()
    {
        using (var without = await RenderAsync(SampleModel()))
        {
            CompactText(without).Should().NotContain("Credit");
        }

        using var with = await RenderAsync(SampleModel() with { Credit = 3.00m, Total = 12.00m });
        CompactText(with).Should().Contain("Credit");
        CompactText(with).Should().Contain("-$3.00");
    }

    /// <summary>A description with no spaces is broken inside its column rather than running into Qty.</summary>
    [Fact]
    public async Task Render_HardBreaksAnUnbrokenWord()
    {
        var unbroken = new string('W', 300);
        var model = SampleModel() with { Lines = [new InvoicePdfLine(unbroken, 1, 12.50m, 12.50m)] };

        using var document = await RenderAsync(model);

        // The description column's text ends at margin + column width - gap: 40 + 285 - 8.
        const double descriptionRight = 317;
        var pieces = document.GetPage(1).GetWords().Where(w => w.Text.All(c => c == 'W') && w.Text.Length > 0).ToList();
        pieces.Sum(w => w.Text.Length).Should().Be(300);
        pieces.Should().OnlyContain(w => w.BoundingBox.Right <= descriptionRight);
    }

    /// <summary>Amounts follow the currency's prefix, suffix and decimal places, grouped the invariant way.</summary>
    [Fact]
    public async Task Render_WritesAmountsInTheCurrencysFormat()
    {
        using var dollars = await RenderAsync(SampleModel() with
        {
            Lines = [new InvoicePdfLine("Dedicated server", 1, 1234.5m, 1234.5m)],
        });
        using var drams = await RenderAsync(SampleModel() with
        {
            Lines = [new InvoicePdfLine("Dedicated server", 1, 12m, 12m)],
            Money = new InvoicePdfMoneyFormat(string.Empty, " ֏", 0),
        });

        CompactText(dollars).Should().Contain("$1,234.50");
        CompactText(drams).Should().Contain("12֏").And.NotContain("12.00");
    }

    /// <summary>An invoice too long for one page carries on, with the table header and page numbers on each.</summary>
    [Fact]
    public async Task Render_BreaksLongInvoiceOntoSecondPage()
    {
        var lines = Enumerable.Range(1, 60).Select(i => new InvoicePdfLine($"Service {i}", 1, 1m, 1m)).ToList();

        using var document = await RenderAsync(SampleModel() with { Lines = lines });

        var total = document.NumberOfPages;
        total.Should().BeGreaterThan(1);
        Compact(document.GetPage(1).Text).Should().Contain($"Page1of{total}");
        foreach (var page in document.GetPages().Skip(1))
        {
            var text = Compact(page.Text);
            if (text.Contains("Service", StringComparison.Ordinal))
            {
                text.Should().Contain("Description", "the table header repeats where rows continue, page {0}", page.Number);
            }

            text.Should().Contain($"Page{page.Number}of{total}");
        }

        CompactText(document).Should().Contain("Service60");
        Compact(document.GetPage(total).Text).Should().Contain("Total");
    }

    /// <summary>A paid invoice is stamped PAID with the date it was paid.</summary>
    [Fact]
    public async Task Render_StampsPaidWithDate()
    {
        var model = SampleModel() with
        {
            Status = InvoiceStatus.Paid,
            PaidAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
        };

        using var document = await RenderAsync(model);

        var text = Compact(document.GetPage(1).Text);
        text.Should().Contain("PAID");
        text.Should().Contain("2026-10-20");
    }

    /// <summary>A draft invoice is stamped DRAFT.</summary>
    [Fact]
    public async Task Render_StampsDraft()
    {
        using var document = await RenderAsync(SampleModel() with { Status = InvoiceStatus.Draft });

        CompactText(document).Should().Contain("DRAFT");
    }

    /// <summary>A cancelled invoice is stamped CANCELLED.</summary>
    [Fact]
    public async Task Render_StampsCancelled()
    {
        using var document = await RenderAsync(SampleModel() with { Status = InvoiceStatus.Cancelled });

        CompactText(document).Should().Contain("CANCELLED");
    }

    /// <summary>A refunded invoice is stamped REFUNDED.</summary>
    [Fact]
    public async Task Render_StampsRefunded()
    {
        using var document = await RenderAsync(SampleModel() with { Status = InvoiceStatus.Refunded });

        CompactText(document).Should().Contain("REFUNDED");
    }

    /// <summary>A partially refunded invoice is stamped PARTIALLY REFUNDED, not PAID.</summary>
    [Fact]
    public async Task Render_StampsPartiallyRefunded()
    {
        using var document = await RenderAsync(SampleModel() with
        {
            Status = InvoiceStatus.PartiallyRefunded,
            PaidAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
        });

        var text = CompactText(document);
        text.Should().Contain("PARTIALLYREFUNDED");
        text.Should().NotContain("PAID2026-10-20");
    }

    /// <summary>An unpaid invoice carries no stamp.</summary>
    [Fact]
    public async Task Render_NoStampForUnpaid()
    {
        using var document = await RenderAsync(SampleModel());

        var text = CompactText(document);
        text.Should().NotContain("PAID");
        text.Should().NotContain("DRAFT");
        text.Should().NotContain("CANCELLED");
        text.Should().NotContain("REFUNDED");
    }

    /// <summary>A logo uploaded to the branding directory is drawn on the first page.</summary>
    [Fact]
    public async Task Render_DrawsLocalLogo()
    {
        WriteFile("uploads/branding/abc/logo.png", Png(200, 80));

        using var document = await RenderAsync(SampleModel() with { LogoPath = "/uploads/branding/abc/logo.png" });

        document.GetPage(1).GetImages().Should().HaveCount(1);
    }

    /// <summary>
    /// With a logo the seller block is taller than the dates beside it, and the bill-to block
    /// still starts below all of it rather than printing over the seller's last line.
    /// </summary>
    [Fact]
    public async Task Render_StartsBillToBelowSellerWithLogo()
    {
        WriteFile("uploads/branding/abc/logo.png", Png(320, 90));

        using var document = await RenderAsync(SampleModel() with
        {
            LogoPath = "/uploads/branding/abc/logo.png",
            Seller = new InvoicePdfParty("Fairhaven Hosting", ["1 Market Street", "Fairhaven 0010", "Armenia", "Tax ID: 123456"]),
        });

        // PdfPig's y grows upwards: below means a smaller top.
        var words = document.GetPage(1).GetWords().ToList();
        var sellerLast = words.Single(w => w.Text == "123456");
        var billTo = words.First(w => w.Text == "Bill");
        billTo.BoundingBox.Top.Should().BeLessThan(sellerLast.BoundingBox.Bottom);
    }

    /// <summary>
    /// A logo file the process cannot read (say, a restored volume with the wrong owner) prints the
    /// invoice without a logo rather than failing every download.
    /// </summary>
    [Fact]
    public async Task Render_SkipsUnreadableLogo()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteFile("uploads/branding/locked/logo.png", Png(200, 80));
        var path = Path.Combine(_webRoot, "web", "uploads/branding/locked/logo.png");
        File.SetUnixFileMode(path, UnixFileMode.None);
        bool readable;
        try
        {
            using var probe = File.OpenRead(path);
            readable = true;
        }
        catch (UnauthorizedAccessException)
        {
            readable = false;
        }

        if (readable)
        {
            // Running as root: the file is readable after all, so there is nothing to test.
            return;
        }

        using var document = await RenderAsync(SampleModel() with { LogoPath = "/uploads/branding/locked/logo.png" });

        document.GetPage(1).GetImages().Should().BeEmpty();
    }

    /// <summary>
    /// A logo setting that is external, escapes the branding directory, names a missing file or
    /// names a file that is not an image prints the invoice without a logo instead of failing.
    /// </summary>
    /// <param name="logoPath">The <c>portal.logo</c> value.</param>
    [Theory]
    [InlineData("https://cdn.example.com/logo.png")]
    [InlineData("/uploads/branding/../../secret.png")]
    [InlineData("/uploads/branding/missing/logo.png")]
    [InlineData("/uploads/branding/corrupt/logo.png")]
    [InlineData("/somewhere-else/logo.png")]
    public async Task Render_SkipsUnusableLogo(string logoPath)
    {
        // Real files behind the traversal and outside-the-directory cases, so only the guard stops them.
        WriteFile("../secret.png", Png(200, 80));
        WriteFile("secret.png", Png(200, 80));
        WriteFile("somewhere-else/logo.png", Png(200, 80));
        WriteFile("uploads/branding/corrupt/logo.png", "not an image"u8.ToArray());

        using var document = await RenderAsync(SampleModel() with { LogoPath = logoPath });

        document.GetPage(1).GetImages().Should().BeEmpty();
    }

    /// <summary>Writes a file under the test's web root, creating its directory.</summary>
    /// <param name="relativePath">Path below the web root.</param>
    /// <param name="content">The bytes to write.</param>
    private void WriteFile(string relativePath, byte[] content)
    {
        var path = Path.GetFullPath(Path.Combine(_webRoot, "web", relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    /// <summary>Draws a solid PNG.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// A one-page invoice stays small enough to email: only the glyphs it uses are embedded. The
    /// regular and bold DejaVu files alone would add about 170 KB, so embedding them whole fails.
    /// </summary>
    [Fact]
    public async Task Render_StaysSmallEnoughToEmail()
    {
        var bytes = await Create().RenderAsync(SampleModel() with { Status = InvoiceStatus.Paid, PaidAt = DateTimeOffset.UtcNow }, CancellationToken.None);

        bytes.Length.Should().BeLessThan(60_000);
    }

    /// <summary>A zero-decimal Armenian amount prints with its dram sign.</summary>
    [Fact]
    public async Task Render_PrintsDramSign()
    {
        var model = SampleModel() with { Money = new InvoicePdfMoneyFormat(string.Empty, " ֏", 0), Total = 6000m };

        using var document = await RenderAsync(model);

        CompactText(document).Should().Contain("6,000֏");
    }

    /// <summary>
    /// Invoices rendered side by side on different threads each come out with their own text, the
    /// fallback-font glyphs included: the fonts used to tell which face has a glyph are not shared
    /// between renders.
    /// </summary>
    [Fact]
    public async Task Render_InParallel_KeepsEveryInvoiceIntact()
    {
        const int invoices = 16;
        var renderer = Create();

        var rendered = await Task.WhenAll(Enumerable.Range(1, invoices).Select(id => Task.Run(async () =>
        {
            var model = SampleModel() with
            {
                InvoiceId = id,
                Money = new InvoicePdfMoneyFormat(string.Empty, " ֏", 0),
                Lines = Enumerable.Range(1, 20).Select(i => new InvoicePdfLine($"Service {id}-{i} Ծաղկաձոր", 1, 1m, 1m)).ToList(),
            };
            return (Id: id, Bytes: await renderer.RenderAsync(model, CancellationToken.None));
        })));

        foreach (var (id, bytes) in rendered)
        {
            using var document = PdfDocument.Open(bytes);
            var text = CompactText(document);
            text.Should().Contain($"Invoice#{id}");
            text.Should().Contain($"Service{id}-20Ծաղկաձոր");
            text.Should().Contain("15֏");
        }
    }
}
