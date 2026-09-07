namespace Innovayse.Application.Admin.Interfaces;

using Innovayse.Application.Admin.Common;

/// <summary>
/// Persists rendered branding files and reports the URLs they are served from.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IBrandingImageProcessor"/> because the two fail for unrelated
/// reasons and are swapped for unrelated reasons: rendering is CPU work on bytes, storage is a
/// filesystem -- or, on a deployment that outgrows one node, an object store. The use case needs
/// both and should depend on neither implementation.
/// </para>
/// <para>
/// The URLs this returns are root-relative (<c>/uploads/branding/...</c>) rather than absolute.
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
    Task<IReadOnlyList<BrandingAssetDto>> SaveAsync(
        BrandingKind kind,
        IReadOnlyList<BrandingRendition> renditions,
        CancellationToken ct);
}
