namespace Innovayse.Infrastructure.Branding.Options;

/// <summary>
/// Where branding uploads are written and how far they are allowed to go.
/// </summary>
/// <remarks>
/// The ceilings live in configuration rather than as constants because they are a deployment's
/// call, not this code's: a single-tenant install on a large box can afford a bigger source
/// image than a shared one, and the pixel ceiling is the only thing standing between an
/// authenticated admin and a decompression bomb that exhausts the container's memory.
/// </remarks>
public sealed class BrandingOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "Branding";

    /// <summary>
    /// Directory the files are written to, relative to the web root.
    /// <para>
    /// Must be backed by a named volume in production. The web root lives inside the image, so
    /// on a deployment without one every uploaded logo is deleted by the next deploy.
    /// </para>
    /// </summary>
    public string RelativePath { get; init; } = "uploads/branding";

    /// <summary>Largest upload accepted, in bytes, before anything is decoded.</summary>
    public int MaxBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Largest edge, in pixels, this deployment will decode.
    /// <para>
    /// Checked from the codec header <b>before</b> the pixels are read, which is the point: a
    /// few hundred kilobytes of highly compressible PNG can declare a 100&#160;000-pixel side and
    /// only become forty gigabytes once decoded.
    /// </para>
    /// </summary>
    public int MaxSourceEdge { get; init; } = 8000;

    /// <summary>
    /// Largest total pixel count this deployment will decode.
    /// </summary>
    /// <remarks>
    /// The per-edge ceiling narrows the decompression-bomb hole; it does not close it. An
    /// 8000&#215;8000 upload satisfies <see cref="MaxSourceEdge"/> and still allocates
    /// 8000&#160;&#215;&#160;8000&#160;&#215;&#160;4 = 256&#160;MB for the pixel buffer alone,
    /// before a single rendition is cloned — and the API container is capped at 1536&#160;MB, so
    /// three concurrent uploads inside the per-edge limit are enough to kill it. Area is the
    /// dimension that maps to memory, so area is what is capped.
    /// </remarks>
    public long MaxSourcePixels { get; init; } = 40_000_000;

    /// <summary>Largest edge of the stored logo. Larger uploads are downscaled to fit.</summary>
    public int MaxLogoEdge { get; init; } = 1024;
}
