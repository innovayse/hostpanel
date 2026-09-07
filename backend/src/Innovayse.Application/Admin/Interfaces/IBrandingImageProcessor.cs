namespace Innovayse.Application.Admin.Interfaces;

using Innovayse.Application.Admin.Common;

/// <summary>
/// Decides what an uploaded branding file really is, and renders the set of files the storefront
/// needs from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This port is the trust boundary for uploaded bytes.</b> Everything above it -- the
/// controller, the handler -- treats an upload as an opaque byte array with an untrusted name.
/// The implementation is the only place that decodes it, and it decides the format from the
/// content, never from the request's <c>Content-Type</c> or the filename's extension. A caller
/// that reaches a <see cref="BrandingRendition"/> is holding bytes this layer has re-encoded
/// itself, which is what makes the stored file safe to serve.
/// </para>
/// <para>
/// Named for the work rather than the library. The implementation today is ImageSharp, pinned
/// to its Apache-2.0 2.x line because this repository is public; nothing in Application should
/// have to change if that becomes something else.
/// </para>
/// </remarks>
public interface IBrandingImageProcessor
{
    /// <summary>
    /// Validates the upload and renders every file the given <paramref name="kind"/> needs.
    /// </summary>
    /// <param name="source">The uploaded bytes and their untrusted name.</param>
    /// <param name="kind">Whether this is the logo or the favicon.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The renditions to store, always including exactly one <see cref="BrandingRole.Primary"/>.
    /// For <see cref="BrandingKind.Favicon"/> this is the full browser, iOS and Android set; for
    /// <see cref="BrandingKind.Logo"/> it is the re-encoded image alone.
    /// </returns>
    /// <exception cref="InvalidBrandingImageException">
    /// The bytes are not an image, or not one of the accepted formats.
    /// </exception>
    /// <exception cref="BrandingImageTooLargeException">
    /// The image decodes to more pixels than this deployment will rasterise.
    /// </exception>
    Task<IReadOnlyList<BrandingRendition>> RenderAsync(
        BrandingSource source,
        BrandingKind kind,
        CancellationToken ct);
}
