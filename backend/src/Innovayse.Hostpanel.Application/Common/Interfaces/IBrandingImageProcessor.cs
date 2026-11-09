using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Decides what an uploaded branding file really is, and renders the set of files the storefront
/// needs from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This port is the trust boundary for uploaded bytes.</b> Everything above it, the
/// controller, the handler, treats an upload as an opaque byte array with an untrusted name.
/// The implementation is the only place that decodes it, and it decides the format from the
/// content, never from the request's <c>Content-Type</c> or the filename's extension. A caller
/// that reaches a <see cref="BrandingRendition"/> is holding bytes this layer has re-encoded
/// itself, which is what makes the stored file safe to serve.
/// </para>
/// <para>
/// Named for the work rather than the library, so the imaging library can be replaced without
/// any change in Application.
/// </para>
/// </remarks>
public interface IBrandingImageProcessor
{
    /// <summary>
    /// Validates the upload and renders every file the given <paramref name="kind"/> needs.
    /// </summary>
    /// <param name="source">The uploaded bytes and their untrusted name.</param>
    /// <param name="kind">Which branding image this is: the favicon or one of the logo kinds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The renditions to store, always including exactly one <see cref="BrandingRole.Primary"/>.
    /// For <see cref="BrandingKind.Favicon"/> this is the full browser, iOS and Android set; for
    /// every logo kind (<see cref="BrandingKind.Logo"/>, <see cref="BrandingKind.LogoDark"/>,
    /// <see cref="BrandingKind.LogoMark"/>) it is the re-encoded image alone. A failed
    /// <see cref="Result{T}"/> carries <c>BRANDING_IMAGE_INVALID</c> when the bytes are not an
    /// image this deployment will store, or <c>BRANDING_IMAGE_TOO_LARGE</c> when the upload or the
    /// pixels it decodes to are over a ceiling this deployment enforces. The precise cause behind
    /// either code is logged by the implementation, never carried on the <see cref="Result{T}"/>.
    /// </returns>
    public Task<Result<IReadOnlyList<BrandingRendition>>> RenderAsync(
        BrandingSource source,
        BrandingKind kind,
        CancellationToken ct);
}
