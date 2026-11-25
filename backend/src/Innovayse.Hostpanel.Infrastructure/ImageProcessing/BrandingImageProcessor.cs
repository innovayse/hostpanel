using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Innovayse.Hostpanel.Infrastructure.ImageProcessing;

/// <summary>
/// Renders branding uploads with SkiaSharp, deciding the format from the bytes themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every stored file is a PNG this class encoded.</b> Nothing an operator uploads is written
/// back out as it arrived, and that is the security property the class exists for rather than an
/// incidental choice: re-encoding drops every ancillary chunk; EXIF with the photographer's
/// coordinates, colour profiles, and the trailing payload that makes one file a valid image
/// <i>and</i> a valid archive or script at the same time.
/// </para>
/// <para>
/// <b>SVG is refused, not stored.</b> Storing the vector untouched is exactly the thing not to
/// do: an SVG served from the same origin as the panel is a script tag with a picture around it;
/// it carries <c>&lt;script&gt;</c> and event handlers, and a browser runs them, which turns "an
/// admin uploads a logo" into stored XSS against every visitor. So the upload is refused with a
/// readable message and the operator converts to PNG once, rather than the product growing a
/// sanitiser it would have to keep ahead of.
/// </para>
/// <para>
/// The format is decided by <see cref="SKCodec"/>, which reads the container's own header. The
/// multipart part's <c>Content-Type</c> and the filename's extension are both written by the
/// client and are therefore claims, not evidence.
/// </para>
/// <para>
/// This replaced an ImageSharp implementation with the same behaviour: SixLabors.ImageSharp 2.x
/// stopped receiving fixes and 3.x is not under a licence this repository can take. See
/// <c>backend/Directory.Packages.props</c>.
/// </para>
/// </remarks>
public sealed class BrandingImageProcessor : IBrandingImageProcessor
{
    /// <summary>
    /// The square icon set generated from a favicon upload: the file name each rendition is
    /// stored under, its edge in pixels, and what links it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The names are the static root's, not this feature's: the storefront and the admin static
    /// roots carry the same eight file names, and `docs/brand/README.md` is where that list
    /// is written down. An upload has to produce exactly those names, because the pages that
    /// link them are the same pages that link the committed defaults.
    /// </para>
    /// <para>
    /// 16 and 32 are the browser tab and the bookmark bar; 180 is the current iOS home-screen
    /// size and the only one Safari reads; 192 and 512 are the two the web app manifest
    /// specification requires an installable PWA to declare, once as <c>any</c> and again as
    /// <c>maskable</c>.
    /// </para>
    /// </remarks>
    private static readonly FaviconVariant[] FaviconSet =
    [
        new("favicon-16x16.png", 16, BrandingRole.Icon),
        new("favicon-32x32.png", 32, BrandingRole.Icon),
        new("apple-touch-icon.png", 180, BrandingRole.AppleTouch),
        new("icon-192.png", 192, BrandingRole.Android),
        new("icon-512.png", 512, BrandingRole.Android),
        new("icon-maskable-192.png", 192, BrandingRole.Maskable),
        new("icon-maskable-512.png", 512, BrandingRole.Maskable),
    ];

    /// <summary>
    /// Formats this deployment accepts.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than "whatever the codec can decode". Skia also reads ICO, WBMP and
    /// several camera raw formats, which no browser renders as a favicon: accepting them would
    /// let an operator upload a file that stores fine and then fails to display, with nothing
    /// explaining why.
    /// </remarks>
    private static readonly HashSet<SKEncodedImageFormat> AcceptedFormats =
    [
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Gif,
        SKEncodedImageFormat.Webp,
        SKEncodedImageFormat.Bmp,
    ];

    /// <summary>
    /// Resampling for every resize: trilinear, i.e. bilinear between mipmap levels.
    /// </summary>
    /// <remarks>
    /// Not bicubic. Skia's cubic filters are fixed 4-tap kernels that ignore mipmaps, so a
    /// 1024px mark reduced to a 16px favicon samples a handful of source pixels and aliases:
    /// fine stripes came out as the wrong overall brightness and moire. Mipmapped sampling
    /// averages the whole footprint, which is what a large downscale needs.
    /// </remarks>
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Resolved ceilings and paths.</summary>
    private readonly BrandingOptions _options;

    /// <summary>Logs the precise reason a refusal was reached (which check failed, whether the SVG parsed), detail that is deliberately never returned to the caller.</summary>
    private readonly ILogger<BrandingImageProcessor> _logger;

    /// <summary>Initializes a new instance of the <see cref="BrandingImageProcessor"/> class.</summary>
    /// <param name="options">Ceilings and paths for branding uploads.</param>
    /// <param name="logger">Logs the precise reason a refusal was reached (which check failed, whether the SVG parsed), detail that is deliberately never returned to the caller.</param>
    public BrandingImageProcessor(IOptions<BrandingOptions> options, ILogger<BrandingImageProcessor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<BrandingRendition>>> RenderAsync(
        BrandingSource source,
        BrandingKind kind,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ct.ThrowIfCancellationRequested();

        // At most one byte past the ceiling is ever read: enough to tell "too large" apart from
        // "exactly at the ceiling" without buffering whatever size the client chose to send.
        var content = await ReadCappedAsync(source.Content, _options.MaxBytes + 1, ct);
        if (content.Length == 0)
        {
            return Invalid("Upload was empty.");
        }

        if (content.Length > _options.MaxBytes)
        {
            return TooLarge($"Upload was over the ceiling of {_options.MaxBytes} bytes.");
        }

        var decoded = Decode(content);
        if (decoded.IsFailure)
        {
            return Result<IReadOnlyList<BrandingRendition>>.FailureFrom(decoded);
        }

        using var image = decoded.Value!;

        IReadOnlyList<BrandingRendition> renditions = kind == BrandingKind.Favicon
            ? RenderFaviconSet(image)
            : RenderLogo(image);

        return Result<IReadOnlyList<BrandingRendition>>.Success(renditions);
    }

    /// <summary>Reads a stream into memory, stopping once <paramref name="limit"/> bytes are held.</summary>
    /// <param name="content">The uploaded stream.</param>
    /// <param name="limit">The most bytes to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bytes read, never more than <paramref name="limit"/>.</returns>
    private static async Task<byte[]> ReadCappedAsync(Stream content, int limit, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (buffer.Length < limit)
        {
            var wanted = (int)Math.Min(chunk.Length, limit - buffer.Length);
            var read = await content.ReadAsync(chunk.AsMemory(0, wanted), ct);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Turns uploaded bytes into an image, or refuses them.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <returns>
    /// The decoded image (unpremultiplied RGBA), which the caller owns, or a failed
    /// <see cref="Result{T}"/> carrying <c>BRANDING_IMAGE_INVALID</c> or <c>BRANDING_IMAGE_TOO_LARGE</c>.
    /// </returns>
    private Result<SKBitmap> Decode(byte[] content)
    {
        // Named before anything else, because it is the one refusal an operator will hit by
        // accident (logos are often kept as SVG), so the message has to say what to do.
        if (LooksLikeSvg(content))
        {
            return Invalid<SKBitmap>("Upload is SVG. Vectors are not stored: convert to PNG before uploading.");
        }

        // Magic bytes first, so a container outside the allow-list (TIFF above all) never reaches
        // a decoder at all. Creating the codec below would otherwise run every registered decoder's
        // header parser (TIFF, ICO, raw) on attacker-controlled bytes just to learn the format is one we then refuse.
        if (!HasAcceptedSignature(content))
        {
            return Invalid<SKBitmap>("Upload does not start with a PNG, JPEG, GIF, WebP or BMP signature.");
        }

        using var stream = new SKMemoryStream(content);

        // Creating the codec reads the header only. This is the decompression-bomb check, and it
        // has to happen before the decode below: a highly compressible PNG a few hundred
        // kilobytes in size can declare a 100 000-pixel side and become forty gigabytes once its
        // pixels are materialised, so a byte ceiling alone lets one upload exhaust the container.
        using var codec = SKCodec.Create(stream);
        if (codec is null)
        {
            return Invalid<SKBitmap>("No decoder recognised the upload.");
        }

        if (!AcceptedFormats.Contains(codec.EncodedFormat))
        {
            return Invalid<SKBitmap>($"Format '{codec.EncodedFormat}' is not accepted here.");
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;

        if (width <= 0 || height <= 0)
        {
            return Invalid<SKBitmap>("Upload declares an empty image.");
        }

        if (width > _options.MaxSourceEdge || height > _options.MaxSourceEdge)
        {
            return DimensionsTooLarge<SKBitmap>(
                width, height, $"Source declares {width}x{height}; ceiling is {_options.MaxSourceEdge} per edge.");
        }

        // Area, not just edges. The buffer below is width * height * 4 bytes, so a shape that
        // clears both edge limits can still be the allocation that ends the process.
        var pixels = (long)width * height;
        if (pixels > _options.MaxSourcePixels)
        {
            return DimensionsTooLarge<SKBitmap>(
                width, height, $"Source declares {pixels} pixels; ceiling is {_options.MaxSourcePixels}.");
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        // GetPixels decodes the first frame only, which is what a logo or icon from an animated
        // GIF or WebP should be. IncompleteInput is a truncated file whose top rows decoded:
        // refused like any other broken upload rather than stored half grey.
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            return Invalid<SKBitmap>($"Header parsed but decoding failed: {result}.");
        }

        return Result<SKBitmap>.Success(bitmap);
    }

    /// <summary>
    /// Whether the bytes open with the signature of one of the five accepted containers.
    /// </summary>
    /// <param name="c">The uploaded bytes.</param>
    /// <returns><see langword="true"/> for PNG, JPEG, GIF, WebP or BMP magic bytes.</returns>
    private static bool HasAcceptedSignature(ReadOnlySpan<byte> c)
    {
        var png = c.Length >= 4 && c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47;
        var jpeg = c.Length >= 3 && c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF;
        var gif = c.Length >= 4 && c[0] == (byte)'G' && c[1] == (byte)'I' && c[2] == (byte)'F' && c[3] == (byte)'8';
        var webp = c.Length >= 12 && c[..4].SequenceEqual("RIFF"u8) && c.Slice(8, 4).SequenceEqual("WEBP"u8);
        var bmp = c.Length >= 2 && c[0] == (byte)'B' && c[1] == (byte)'M';

        return png || jpeg || gif || webp || bmp;
    }

    /// <summary>Logs why an upload was refused as not an acceptable image, and answers the code for it.</summary>
    /// <param name="reason">The precise cause, written to the log and never returned.</param>
    /// <returns>A failed <see cref="Result{T}"/> carrying <c>BRANDING_IMAGE_INVALID</c>.</returns>
    private Result<IReadOnlyList<BrandingRendition>> Invalid(string reason) =>
        Invalid<IReadOnlyList<BrandingRendition>>(reason);

    /// <summary>Logs why an upload was refused as not an acceptable image, and answers the code for it.</summary>
    /// <typeparam name="T">The value type the caller's <see cref="Result{T}"/> carries on success.</typeparam>
    /// <param name="reason">The precise cause, written to the log and never returned.</param>
    /// <returns>A failed <see cref="Result{T}"/> carrying <c>BRANDING_IMAGE_INVALID</c>.</returns>
    private Result<T> Invalid<T>(string reason)
    {
        _logger.LogWarning("Branding upload refused: {Reason}", reason);
        return Result<T>.FailureCode(HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>Logs why an upload was refused for exceeding the byte ceiling, and answers the code for it.</summary>
    /// <param name="reason">Which ceiling was hit, written to the log and never returned.</param>
    /// <returns>A failed <see cref="Result{T}"/> carrying <c>BRANDING_IMAGE_TOO_LARGE</c>.</returns>
    private Result<IReadOnlyList<BrandingRendition>> TooLarge(string reason)
    {
        _logger.LogWarning("Branding upload exceeded the byte ceiling: {Reason}", reason);
        return Result<IReadOnlyList<BrandingRendition>>.FailureCode(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
    }

    /// <summary>
    /// Logs why an upload was refused for declaring too many pixels, and answers the code for it with the
    /// declared size, so the operator sees what to resize rather than a bare "too large".
    /// </summary>
    /// <typeparam name="T">The value type the caller's <see cref="Result{T}"/> carries on success.</typeparam>
    /// <param name="width">The declared width in pixels.</param>
    /// <param name="height">The declared height in pixels.</param>
    /// <param name="reason">Which ceiling was hit, written to the log.</param>
    /// <returns>A failed <see cref="Result{T}"/> carrying <c>BRANDING_IMAGE_DIMENSIONS_TOO_LARGE</c> and <c>errors.dimensions</c>.</returns>
    private Result<T> DimensionsTooLarge<T>(int width, int height, string reason)
    {
        _logger.LogWarning("Branding upload exceeded a pixel ceiling: {Reason}", reason);
        return Result<T>.FailureWithDetail(
            reason, HostpanelErrorCodes.BRANDING_IMAGE_DIMENSIONS_TOO_LARGE, "dimensions", $"{width}x{height}");
    }

    /// <summary>Renders the full square icon set from a favicon upload.</summary>
    /// <param name="source">The decoded upload.</param>
    /// <returns>The primary rendition plus every size in the set.</returns>
    private static List<BrandingRendition> RenderFaviconSet(SKBitmap source)
    {
        var renditions = new List<BrandingRendition>(FaviconSet.Length + 1);

        // The primary is the largest ordinary icon rather than the upload as it arrived: it is
        // what a browser with no size preference picks, and it has to be square like the rest.
        // Named favicon.png, not favicon.ico: this process encodes PNG, and every surface that
        // links the uploaded set links it by this name.
        var largest = FaviconSet.Where(f => f.Role != BrandingRole.Maskable).Max(f => f.Size);
        renditions.Add(new BrandingRendition(
            "favicon.png", EncodeSquare(source, largest), "image/png", largest, BrandingRole.Primary));

        foreach (var (file, size, role) in FaviconSet)
        {
            var bytes = role == BrandingRole.Maskable
                ? EncodeMaskable(source, size)
                : EncodeSquare(source, size);

            renditions.Add(new BrandingRendition(file, bytes, "image/png", size, role));
        }

        return renditions;
    }

    /// <summary>Renders the single stored logo, downscaled to fit if needed.</summary>
    /// <param name="source">The decoded upload.</param>
    /// <returns>A one-element list holding the primary rendition.</returns>
    private List<BrandingRendition> RenderLogo(SKBitmap source)
    {
        // Downscale only: a 512px mark handed to a 1024px box is stored at 512, not enlarged
        // into something blurrier and several times the bytes.
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= _options.MaxLogoEdge)
        {
            return
            [
                new BrandingRendition("logo.png", Encode(source), "image/png", null, BrandingRole.Primary),
            ];
        }

        // Fit inside the box, keeping the aspect ratio.
        var scale = (double)_options.MaxLogoEdge / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var logo = NewCanvasBitmap(width, height);
        using (var canvas = new SKCanvas(logo))
        {
            DrawFitted(canvas, source, new SKRect(0, 0, width, height));
        }

        return
        [
            new BrandingRendition("logo.png", Encode(logo), "image/png", null, BrandingRole.Primary),
        ];
    }

    /// <summary>
    /// Draws the source centred on a transparent square of the given edge, preserving aspect
    /// ratio, and encodes it as PNG.
    /// </summary>
    /// <remarks>
    /// Padded rather than stretched. An icon is square by definition, and a wide logo squashed
    /// into 512&#215;512 is the failure operators notice immediately. The padding is transparent,
    /// so a logo on a dark header does not gain a white box.
    /// </remarks>
    /// <param name="source">The decoded upload.</param>
    /// <param name="edge">Edge length of the output square, in pixels.</param>
    /// <returns>PNG bytes.</returns>
    private static byte[] EncodeSquare(SKBitmap source, int edge)
    {
        using var square = NewCanvasBitmap(edge, edge);
        using (var canvas = new SKCanvas(square))
        {
            canvas.Clear(SKColors.Transparent);
            DrawFitted(canvas, source, new SKRect(0, 0, edge, edge));
        }

        return Encode(square);
    }

    /// <summary>
    /// Renders the maskable variant: the upload held at 70% on a full-bleed square.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android crops an adaptive icon to whatever silhouette the launcher chooses and
    /// guarantees only the inner 80%. The ordinary rendition cannot be reused: its transparent
    /// margin would be cropped into a smaller, lopsided square. So the canvas is filled edge to
    /// edge and the artwork sits well inside the safe circle, at 70% of the canvas.
    /// </para>
    /// <para>
    /// The field is taken from the upload's own top-left pixel rather than a colour chosen
    /// here. An operator's mark usually sits on its own tile, and sampling it keeps the padded
    /// area continuous with the artwork instead of introducing a colour the brand never had. A
    /// fully transparent corner falls back to white, because a maskable icon may not be
    /// transparent: the launcher would fill the crop with whatever it liked. A partly
    /// transparent corner is used fully opaque, for the same reason.
    /// </para>
    /// </remarks>
    /// <param name="source">The decoded upload.</param>
    /// <param name="edge">Edge length of the output square, in pixels.</param>
    /// <returns>PNG bytes.</returns>
    private static byte[] EncodeMaskable(SKBitmap source, int edge)
    {
        var corner = source.GetPixel(0, 0);
        var field = corner.Alpha == 0 ? SKColors.White : corner.WithAlpha(255);
        var inner = (int)Math.Round(edge * 0.7);
        var offset = (edge - inner) / 2f;

        using var bitmap = NewCanvasBitmap(edge, edge);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(field);
            DrawFitted(canvas, source, new SKRect(offset, offset, offset + inner, offset + inner));
        }

        return Encode(bitmap);
    }

    /// <summary>
    /// Draws the source inside a box, as large as fits without distortion, centred.
    /// </summary>
    /// <param name="canvas">Where to draw.</param>
    /// <param name="source">The decoded upload.</param>
    /// <param name="box">The area to fit into.</param>
    private static void DrawFitted(SKCanvas canvas, SKBitmap source, SKRect box)
    {
        var scale = Math.Min(box.Width / source.Width, box.Height / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        var left = box.Left + ((box.Width - width) / 2f);
        var top = box.Top + ((box.Height - height) / 2f);

        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, new SKRect(left, top, left + width, top + height), Sampling, paint);
    }

    /// <summary>A blank RGBA bitmap in the format every rendition is drawn into.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>A bitmap; the caller owns it.</returns>
    private static SKBitmap NewCanvasBitmap(int width, int height) =>
        new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

    /// <summary>Encodes an image as PNG.</summary>
    /// <param name="bitmap">The image to encode.</param>
    /// <returns>PNG bytes.</returns>
    private static byte[] Encode(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        using var output = new MemoryStream();

        // Icons are flat colour and text, which is what adaptive filtering and a full zlib pass
        // are for. These files are written once and served for a year, so the encode cost is
        // paid once and the transfer saving is permanent.
        var encoded = pixmap.Encode(
            output,
            new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, zLibLevel: 9));

        if (!encoded)
        {
            throw new InvalidOperationException("PNG encoding failed.");
        }

        return output.ToArray();
    }

    /// <summary>
    /// Whether the bytes look like SVG source.
    /// </summary>
    /// <remarks>
    /// A prefix scan rather than a parse. It exists only to turn what would otherwise be a
    /// generic "no decoder recognised this" into a message that tells the operator what to do,
    /// so a loose match costs nothing: anything it misses is refused a line later anyway.
    /// </remarks>
    /// <param name="content">The uploaded bytes.</param>
    /// <returns><see langword="true"/> when the leading text opens an SVG document.</returns>
    private static bool LooksLikeSvg(byte[] content)
    {
        // Enough to clear a UTF-8 BOM, an XML declaration, a doctype and leading whitespace.
        var prefixLength = Math.Min(content.Length, 1024);
        var prefix = System.Text.Encoding.UTF8.GetString(content, 0, prefixLength);

        return prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }
}
