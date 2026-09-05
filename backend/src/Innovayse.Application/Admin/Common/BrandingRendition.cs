namespace Innovayse.Application.Admin.Common;

/// <summary>
/// One file produced from an upload, in memory and not yet stored.
/// </summary>
/// <param name="FileName">
/// Generated name, e.g. <c>android-chrome-192x192.png</c>. Chosen by the processor rather than
/// derived from the upload, so nothing the operator typed reaches a path.
/// </param>
/// <param name="Content">The encoded image bytes.</param>
/// <param name="ContentType">The real media type of <paramref name="Content"/>.</param>
/// <param name="PixelSize">
/// Edge length in pixels for a square icon; <see langword="null"/> for a rendition whose size is
/// not part of its contract, such as the logo or a vector favicon.
/// </param>
/// <param name="Role">What the storefront links this rendition as.</param>
public sealed record BrandingRendition(
    string FileName,
    byte[] Content,
    string ContentType,
    int? PixelSize,
    BrandingRole Role);
