namespace Innovayse.Infrastructure.Branding;

using Innovayse.Application.Admin.Common;
using Innovayse.Application.Admin.Interfaces;
using Innovayse.Infrastructure.Branding.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

/// <summary>
/// Renders branding uploads with ImageSharp, deciding the format from the bytes themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every stored file is a PNG this class encoded.</b> Nothing an operator uploads is written
/// back out as it arrived, and that is the security property the class exists for rather than an
/// incidental choice: re-encoding drops every ancillary chunk — EXIF with the photographer's
/// coordinates, colour profiles, and the trailing payload that makes one file a valid image
/// <i>and</i> a valid archive or script at the same time.
/// </para>
/// <para>
/// <b>SVG is refused, not stored.</b> ImageSharp is a raster library and cannot rasterise a
/// vector, and storing the vector untouched is exactly the thing not to do: an SVG served from
/// the same origin as the panel is a script tag with a picture around it — it carries
/// <c>&lt;script&gt;</c> and event handlers, and a browser runs them, which turns "an admin
/// uploads a logo" into stored XSS against every visitor. So the upload is refused with a
/// readable message and the operator converts to PNG once, rather than the product growing a
/// sanitiser it would have to keep ahead of.
/// </para>
/// <para>
/// The format is decided by <see cref="Image.Identify(System.IO.Stream)"/>, which reads the
/// container's own header. The multipart part's <c>Content-Type</c> and the filename's extension
/// are both written by the client and are therefore claims, not evidence; the previous version of
/// this upload trusted the former as its only check.
/// </para>
/// </remarks>
/// <param name="options">Ceilings and paths for branding uploads.</param>
public sealed class ImageSharpBrandingImageProcessor(IOptions<BrandingOptions> options)
    : IBrandingImageProcessor
{
    /// <summary>
    /// The square icon set generated from a favicon upload, with the role each is linked as.
    /// </summary>
    /// <remarks>
    /// 16 and 32 are the browser tab and the bookmark bar; 48 is what Windows uses for a pinned
    /// site; 180 is the current iOS home-screen size and the only one Safari reads; 192 and 512
    /// are the two the web app manifest specification requires an installable PWA to declare,
    /// and Android picks between them.
    /// </remarks>
    private static readonly (int Size, BrandingRole Role)[] _faviconSet =
    [
        (16, BrandingRole.Icon),
        (32, BrandingRole.Icon),
        (48, BrandingRole.Icon),
        (180, BrandingRole.AppleTouch),
        (192, BrandingRole.Android),
        (512, BrandingRole.Android),
    ];

    /// <summary>
    /// Format identifiers this deployment accepts, as ImageSharp names them.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than "whatever ImageSharp can decode". The library also reads TGA and
    /// TIFF, which no browser renders as a favicon — accepting them would let an operator upload
    /// a file that stores fine and then fails to display, with nothing explaining why.
    /// </remarks>
    private static readonly HashSet<string> _acceptedFormats =
        new(StringComparer.OrdinalIgnoreCase) { "PNG", "JPEG", "GIF", "WebP", "BMP" };

    /// <summary>Resolved ceilings and paths.</summary>
    private readonly BrandingOptions _options = options.Value;

    /// <inheritdoc />
    public Task<IReadOnlyList<BrandingRendition>> RenderAsync(
        BrandingSource source,
        BrandingKind kind,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (source.Content.Length == 0)
        {
            throw new InvalidBrandingImageException("Upload was empty.");
        }

        if (source.Content.Length > _options.MaxBytes)
        {
            throw new BrandingImageTooLargeException(
                $"Upload was {source.Content.Length} bytes; ceiling is {_options.MaxBytes}.");
        }

        using var image = Decode(source.Content);

        IReadOnlyList<BrandingRendition> renditions = kind == BrandingKind.Favicon
            ? RenderFaviconSet(image)
            : RenderLogo(image);

        return Task.FromResult(renditions);
    }

    /// <summary>
    /// Turns uploaded bytes into an image, or refuses them.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <returns>The decoded image; the caller owns it.</returns>
    /// <exception cref="InvalidBrandingImageException">Not a format this accepts.</exception>
    /// <exception cref="BrandingImageTooLargeException">Declares more pixels than allowed.</exception>
    private Image<Rgba32> Decode(byte[] content)
    {
        // Named before anything else, because it is the one refusal an operator will hit by
        // accident: the admin panel used to accept SVG, so the message has to say what to do.
        if (LooksLikeSvg(content))
        {
            throw new InvalidBrandingImageException(
                "Upload is SVG. Vectors are not stored: convert to PNG before uploading.");
        }

        using var probe = new MemoryStream(content, writable: false);

        // Identify reads the header only. This is the decompression-bomb check, and it has to
        // happen before the decode below: a highly compressible PNG a few hundred kilobytes in
        // size can declare a 100 000-pixel side and become forty gigabytes once its pixels are
        // materialised, so a byte ceiling alone lets one upload exhaust the container.
        var info = Image.Identify(probe, out var format)
            ?? throw new InvalidBrandingImageException("No decoder recognised the upload.");

        if (format is null || !_acceptedFormats.Contains(format.Name))
        {
            throw new InvalidBrandingImageException(
                $"Format '{format?.Name ?? "unknown"}' is not accepted here.");
        }

        if (info.Width > _options.MaxSourceEdge || info.Height > _options.MaxSourceEdge)
        {
            throw new BrandingImageTooLargeException(
                $"Source declares {info.Width}x{info.Height}; ceiling is {_options.MaxSourceEdge} per edge.");
        }

        // Area, not just edges. The buffer below is width * height * 4 bytes, so a shape that
        // clears both edge limits can still be the allocation that ends the process.
        var pixels = (long)info.Width * info.Height;
        if (pixels > _options.MaxSourcePixels)
        {
            throw new BrandingImageTooLargeException(
                $"Source declares {pixels} pixels; ceiling is {_options.MaxSourcePixels}.");
        }

        using var decode = new MemoryStream(content, writable: false);
        try
        {
            return Image.Load<Rgba32>(decode);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The header parsed and the pixels did not. Nothing here is worth showing a caller,
            // but the decoder's own message is worth keeping for whoever reads the log.
            throw new InvalidBrandingImageException(
                $"Header parsed but decoding failed: {ex.Message}");
        }
    }

    /// <summary>Renders the full square icon set from a favicon upload.</summary>
    /// <param name="source">The decoded upload.</param>
    /// <returns>The primary rendition plus every size in the set.</returns>
    private static List<BrandingRendition> RenderFaviconSet(Image<Rgba32> source)
    {
        var renditions = new List<BrandingRendition>(_faviconSet.Length + 1);

        // The primary is the largest icon rather than the upload as it arrived: it is what a
        // browser with no size preference picks, and it has to be square like the rest of the set.
        var largest = _faviconSet.Max(f => f.Size);
        renditions.Add(new BrandingRendition(
            "favicon.png", EncodeSquare(source, largest), "image/png", largest, BrandingRole.Primary));

        foreach (var (size, role) in _faviconSet)
        {
            var name = role switch
            {
                BrandingRole.AppleTouch => "apple-touch-icon.png",
                BrandingRole.Android => $"android-chrome-{size}x{size}.png",
                _ => $"favicon-{size}x{size}.png",
            };

            renditions.Add(new BrandingRendition(name, EncodeSquare(source, size), "image/png", size, role));
        }

        return renditions;
    }

    /// <summary>Renders the single stored logo, downscaled to fit if needed.</summary>
    /// <param name="source">The decoded upload.</param>
    /// <returns>A one-element list holding the primary rendition.</returns>
    private List<BrandingRendition> RenderLogo(Image<Rgba32> source)
    {
        // Downscale only. ResizeMode.Max fits the image inside the box in both directions --
        // including upwards, so a 512px mark handed to a 1024px box comes back at 1024,
        // blurrier and several times the bytes for no gain. Measured: a 512x512 upload was
        // stored as 1024x1024 before this guard.
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= _options.MaxLogoEdge)
        {
            return
            [
                new BrandingRendition("logo.png", Encode(source), "image/png", null, BrandingRole.Primary),
            ];
        }

        using var logo = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(_options.MaxLogoEdge, _options.MaxLogoEdge),
            // Max keeps the aspect ratio; the guard above is what stops it enlarging.
            Mode = ResizeMode.Max,
        }));

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
    /// into 512&#215;512 is the failure operators notice immediately.
    /// </remarks>
    /// <param name="source">The decoded upload.</param>
    /// <param name="edge">Edge length of the output square, in pixels.</param>
    /// <returns>PNG bytes.</returns>
    private static byte[] EncodeSquare(Image<Rgba32> source, int edge)
    {
        using var square = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(edge, edge),
            // Pad fits the whole image inside the square and fills the remainder with the colour
            // below — transparent, so a logo on a dark header does not gain a white box.
            Mode = ResizeMode.Pad,
            PadColor = Color.Transparent,
        }));

        return Encode(square);
    }

    /// <summary>Encodes an image as PNG.</summary>
    /// <param name="image">The image to encode.</param>
    /// <returns>PNG bytes.</returns>
    private static byte[] Encode(Image<Rgba32> image)
    {
        using var output = new MemoryStream();

        image.Save(output, new PngEncoder
        {
            // Icons are flat colour and text, which is what this filter and a full compression
            // pass are for. These files are written once and served for a year, so the encode
            // cost is paid once and the transfer saving is permanent.
            ColorType = PngColorType.RgbWithAlpha,
            CompressionLevel = PngCompressionLevel.BestCompression,
        });

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
