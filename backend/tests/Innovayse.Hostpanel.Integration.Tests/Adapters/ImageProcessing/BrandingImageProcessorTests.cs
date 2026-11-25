using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.ImageProcessing;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.ImageProcessing;

/// <summary>
/// Tests for the branding upload renderer.
/// </summary>
/// <remarks>
/// The inputs are drawn in memory with SkiaSharp itself, so no fixture files are needed. What is
/// worth checking is the contract the panel relies on: every output is a PNG this class encoded,
/// at the size the favicon set promises, and every refusal is a <c>BRANDING_IMAGE_*</c> code on the
/// <see cref="Innovayse.Hostpanel.Shared.Models.Result"/>, reached before pixels are decoded where
/// that matters, with the precise reason in the log and nowhere else.
/// </remarks>
public sealed class BrandingImageProcessorTests
{
    /// <summary>The PNG file signature.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Builds a processor with default options, or the given ones.</summary>
    /// <param name="logger">The logger the processor writes its refusal reasons to.</param>
    /// <param name="options">Options to use instead of the defaults.</param>
    /// <returns>The processor under test.</returns>
    private static BrandingImageProcessor Create(
        CapturingLogger<BrandingImageProcessor>? logger = null,
        BrandingOptions? options = null) =>
        new(Options.Create(options ?? new BrandingOptions()), logger ?? new CapturingLogger<BrandingImageProcessor>());

    /// <summary>Renders an upload and asserts that it was accepted.</summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="fileName">The untrusted upload name.</param>
    /// <param name="kind">Which branding image this is.</param>
    /// <param name="options">Options to use instead of the defaults.</param>
    /// <returns>The renditions to store.</returns>
    private static async Task<IReadOnlyList<BrandingRendition>> RenderAcceptedAsync(
        byte[] content, string fileName, BrandingKind kind, BrandingOptions? options = null)
    {
        var result = await Create(options: options).RenderAsync(new BrandingSource(new MemoryStream(content), fileName), kind, default);

        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    /// <summary>Draws a solid image with a contrasting square in the middle and encodes it.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="format">Container format to encode.</param>
    /// <param name="background">Background colour (top-left pixel).</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] MakeImage(
        int width,
        int height,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png,
        SKColor? background = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? new SKColor(0x10, 0x40, 0xC0));
            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(width / 4f, height / 4f, width / 2f, height / 2f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>Decodes rendition bytes back into a bitmap for inspection.</summary>
    /// <param name="png">PNG bytes.</param>
    /// <returns>The decoded bitmap.</returns>
    private static SKBitmap Read(byte[] png) => SKBitmap.Decode(png);

    /// <summary>A favicon upload produces the whole square icon set, every file a PNG.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Favicon_ProducesTheFullSquareSetAsPngAsync()
    {
        var renditions = await RenderAcceptedAsync(MakeImage(300, 120), "logo.png", BrandingKind.Favicon);

        renditions.Select(r => r.FileName).Should().BeEquivalentTo(
        [
            "favicon.png", "favicon-16x16.png", "favicon-32x32.png", "apple-touch-icon.png",
            "icon-192.png", "icon-512.png", "icon-maskable-192.png", "icon-maskable-512.png",
        ]);

        foreach (var rendition in renditions)
        {
            rendition.ContentType.Should().Be("image/png");
            rendition.Content.Take(8).Should().Equal(PngSignature);

            using var bitmap = Read(rendition.Content);
            bitmap.Width.Should().Be(rendition.PixelSize);
            bitmap.Height.Should().Be(rendition.PixelSize, "every icon is square");
        }
    }

    /// <summary>A wide upload is padded with transparency rather than stretched.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Favicon_PadsAWideImageWithTransparencyAsync()
    {
        var renditions = await RenderAcceptedAsync(MakeImage(400, 100), "wide.png", BrandingKind.Favicon);

        using var icon = Read(renditions.Single(r => r.FileName == "icon-512.png").Content);

        icon.GetPixel(256, 0).Alpha.Should().Be(0, "the band above a wide logo is padding");
        icon.GetPixel(256, 256).Alpha.Should().Be(255, "the artwork itself is opaque");
    }

    /// <summary>The maskable icon is filled edge to edge with the upload's top-left colour.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Maskable_FillsTheCanvasWithTheSourceCornerColourAsync()
    {
        var blue = new SKColor(0x10, 0x40, 0xC0);
        var renditions = await RenderAcceptedAsync(
            MakeImage(200, 200, background: blue), "mark.png", BrandingKind.Favicon);

        using var maskable = Read(renditions.Single(r => r.FileName == "icon-maskable-512.png").Content);

        maskable.GetPixel(0, 0).Should().Be(blue);
        maskable.GetPixel(511, 511).Alpha.Should().Be(255, "a maskable icon may not be transparent");
    }

    /// <summary>A transparent corner falls back to a white field, because a maskable icon may not be transparent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Maskable_FallsBackToWhiteForATransparentCornerAsync()
    {
        var renditions = await RenderAcceptedAsync(
            MakeImage(200, 200, background: SKColors.Transparent), "mark.png", BrandingKind.Favicon);

        using var maskable = Read(renditions.Single(r => r.FileName == "icon-maskable-192.png").Content);

        maskable.GetPixel(0, 0).Should().Be(SKColors.White);
    }

    /// <summary>
    /// Large downscales average the source footprint instead of aliasing.
    /// </summary>
    /// <param name="file">The rendition to inspect.</param>
    /// <param name="min">The darkest red value a sampled pixel may have.</param>
    /// <param name="max">The brightest red value a sampled pixel may have.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("favicon-16x16.png", 205, 240)]
    [InlineData("favicon-32x32.png", 205, 240)]
    // At 180px each output pixel covers 5.7 source columns, which hold either one line or
    // none, so even an exact area average ranges from 210 to 255. A 4-tap cubic filter gave
    // 29 to 255 here; the bounds allow the area average and refuse that.
    [InlineData("apple-touch-icon.png", 185, 255)]
    public async Task Favicon_DownscalesFineDetailWithoutAliasingAsync(string file, int min, int max)
    {
        // 1px black lines every 8px on white: the true average brightness is 255 * 7/8 = 223.
        // A filter that samples only a few source pixels lands far from that, or in moire.
        using var bitmap = new SKBitmap(new SKImageInfo(1024, 1024, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            for (var x = 0; x < 1024; x += 8)
            {
                canvas.DrawRect(x, 0, 1, 1024, paint);
            }
        }

        using var source = SKImage.FromBitmap(bitmap);
        using var png = source.Encode(SKEncodedImageFormat.Png, 100);

        var renditions = await RenderAcceptedAsync(png.ToArray(), "stripes.png", BrandingKind.Favicon);

        using var icon = Read(renditions.Single(r => r.FileName == file).Content);
        var values = Enumerable.Range(0, icon.Width).Select(x => (int)icon.GetPixel(x, icon.Height / 2).Red).ToList();

        values.Should().OnlyContain(v => v >= min && v <= max, "fine detail should average out, not alias");
    }

    /// <summary>A logo larger than the edge ceiling is scaled down, keeping its aspect ratio.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Logo_IsDownscaledToTheEdgeLimitKeepingAspectRatioAsync()
    {
        var renditions = await RenderAcceptedAsync(
            MakeImage(1000, 500), "logo.png", BrandingKind.Logo, new BrandingOptions { MaxLogoEdge = 256 });

        var logo = renditions.Should().ContainSingle().Subject;
        logo.FileName.Should().Be("logo.png");

        using var bitmap = Read(logo.Content);
        bitmap.Width.Should().Be(256);
        bitmap.Height.Should().Be(128);
    }

    /// <summary>A logo already inside the ceiling is stored at its own size.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Logo_IsNeverEnlargedAsync()
    {
        var renditions = await RenderAcceptedAsync(MakeImage(300, 100), "small.png", BrandingKind.Logo);

        using var bitmap = Read(renditions.Single().Content);
        bitmap.Width.Should().Be(300);
        bitmap.Height.Should().Be(100);
    }

    /// <summary>JPEG and WebP uploads are accepted and re-encoded as PNG.</summary>
    /// <param name="format">The container the upload arrives in.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task AcceptedFormats_AreReEncodedAsPngAsync(SKEncodedImageFormat format)
    {
        var renditions = await RenderAcceptedAsync(MakeImage(64, 64, format), "upload", BrandingKind.Logo);

        renditions.Single().Content.Take(8).Should().Equal(PngSignature);
    }

    /// <summary>SVG is refused with <c>BRANDING_IMAGE_INVALID</c>, and the log says what to do.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Svg_IsRefusedAndTheLogSaysWhatToDoAsync()
    {
        var svg = "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        var logger = new CapturingLogger<BrandingImageProcessor>();

        var result = await Create(logger).RenderAsync(new BrandingSource(new MemoryStream(svg), "logo.svg"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
        logger.Messages.Should().ContainSingle().Which.Should().Contain("convert to PNG");
    }

    /// <summary>Bytes that are no image at all are refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnrecognisedBytes_AreRefusedAsync()
    {
        var result = await Create().RenderAsync(
            new BrandingSource(new MemoryStream("not an image at all"u8.ToArray()), "x.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>
    /// A container outside the allow-list is refused on its magic bytes, before any decoder's header
    /// parser runs on it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TiffSignature_IsRefusedOnMagicBytesBeforeAnyDecoderAsync()
    {
        // "II*\0" is the little-endian TIFF header; the rest is padding no decoder should ever see.
        byte[] tiff = [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var logger = new CapturingLogger<BrandingImageProcessor>();

        var result = await Create(logger).RenderAsync(new BrandingSource(new MemoryStream(tiff), "scan.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
        logger.Messages.Should().ContainSingle().Which.Should().Contain("signature");
    }

    /// <summary>An empty upload is refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EmptyUpload_IsRefusedAsync()
    {
        var result = await Create().RenderAsync(new BrandingSource(new MemoryStream(), "x.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>An edge over the ceiling is refused from the header, naming the pixel ceiling and the declared size.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OversizedEdge_IsRefusedFromTheHeaderAsync()
    {
        var result = await Create(options: new BrandingOptions { MaxSourceEdge = 100 }).RenderAsync(
            new BrandingSource(new MemoryStream(MakeImage(200, 50)), "big.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_DIMENSIONS_TOO_LARGE);
        result.Errors.Should().ContainKey("dimensions").WhoseValue.Should().Equal("200x50");
    }

    /// <summary>The pixel-area ceiling applies even when both edges fit.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OversizedArea_IsRefusedEvenWhenBothEdgesFitAsync()
    {
        var result = await Create(options: new BrandingOptions { MaxSourceEdge = 1000, MaxSourcePixels = 10_000 })
            .RenderAsync(new BrandingSource(new MemoryStream(MakeImage(200, 200)), "big.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_DIMENSIONS_TOO_LARGE);
        result.Errors.Should().ContainKey("dimensions").WhoseValue.Should().Equal("200x200");
    }

    /// <summary>
    /// A client that keeps sending is cut off one byte past the ceiling: the upload is refused as too
    /// large without being buffered whole.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EndlessUpload_IsReadOnlyToTheCeilingAsync()
    {
        var stream = new EndlessStream();

        var result = await Create(options: new BrandingOptions { MaxBytes = 1000 }).RenderAsync(
            new BrandingSource(stream, "endless.png"), BrandingKind.Logo, default);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
        stream.BytesRead.Should().Be(1001);
    }

    /// <summary>
    /// A small file whose pixels are over the ceiling is never reported as a byte-size problem: the two refusals carry
    /// different codes, so the operator is told which limit the image broke.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SmallFileWithTooManyPixels_IsNotReportedAsTooManyBytesAsync()
    {
        var content = MakeImage(200, 50);

        var result = await Create(options: new BrandingOptions { MaxSourceEdge = 100 }).RenderAsync(
            new BrandingSource(new MemoryStream(content), "small.png"), BrandingKind.Logo, default);

        content.Length.Should().BeLessThan(new BrandingOptions().MaxBytes);
        result.ErrorCode.Should().NotBe(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
    }

    /// <summary>An upload over the byte ceiling is refused before anything is decoded.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OversizedUpload_IsRefusedByByteCountAsync()
    {
        var result = await Create(options: new BrandingOptions { MaxBytes = 10 }).RenderAsync(
            new BrandingSource(new MemoryStream(MakeImage(64, 64)), "x.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
    }

    /// <summary>A truncated file is refused rather than stored half decoded.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TruncatedFile_IsRefusedRatherThanStoredHalfDecodedAsync()
    {
        var png = MakeImage(256, 256);
        var truncated = png.Take(png.Length / 2).ToArray();

        var result = await Create().RenderAsync(new BrandingSource(new MemoryStream(truncated), "cut.png"), BrandingKind.Logo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>Re-encoding drops a payload appended after the image, the classic polyglot trick.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Metadata_IsDroppedByReEncodingAsync()
    {
        // A trailing payload after IEND is the classic "image and archive at once" polyglot.
        var payload = "PK\u0003\u0004 not-an-image-but-a-zip"u8.ToArray();
        var polyglot = MakeImage(64, 64).Concat(payload).ToArray();

        var renditions = await RenderAcceptedAsync(polyglot, "poly.png", BrandingKind.Logo);

        var stored = renditions.Single().Content;
        System.Text.Encoding.ASCII.GetString(stored).Should().NotContain("not-an-image-but-a-zip");
    }
}
