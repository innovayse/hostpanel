using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Persists rendered branding files and reports the URLs they are served from.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IBrandingImageProcessor"/> because the two fail for unrelated
/// reasons and are swapped for unrelated reasons: rendering is CPU work on bytes, storage is a
/// filesystem, or, on a deployment that outgrows one node, an object store. The use case needs
/// both and should depend on neither implementation.
/// </para>
/// <para>
/// The URLs this returns are root-relative (under <c>/uploads/branding/</c>) rather than absolute.
/// The storefront and the admin panel are served from different origins to the API, and both
/// reach these files through their own proxy, so an absolute URL baked in at upload time would
/// be wrong for at least one of them and would survive a domain change as a broken link.
/// </para>
/// </remarks>
public interface IBrandingStorage
{
    /// <summary>
    /// Writes one upload's renditions and returns where they can be read.
    /// </summary>
    /// <param name="kind">Whether this is the logo or the favicon.</param>
    /// <param name="renditions">The files to write, as produced by the processor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// One entry per rendition, in the order given, each carrying its root-relative URL.
    /// </returns>
    public Task<IReadOnlyList<BrandingAssetDto>> SaveAsync(
        BrandingKind kind,
        IReadOnlyList<BrandingRendition> renditions,
        CancellationToken ct);

    /// <summary>
    /// Removes the upload a branding URL points into, every rendition of it included. Called when a
    /// branding setting is replaced, so a superseded upload does not stay on disk.
    /// </summary>
    /// <param name="url">
    /// The setting's previous value. A value this storage did not issue (an external
    /// <c>https://</c> address, a path outside the uploads root) is left alone.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the upload is gone.</returns>
    public Task RemoveAsync(string url, CancellationToken ct);
}
