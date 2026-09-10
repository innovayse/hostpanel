namespace Innovayse.Application.Admin.Common;

/// <summary>
/// Which storefront branding image an upload is for.
/// </summary>
/// <remarks>
/// <para>
/// The kinds differ in more than their label. A logo is rendered at one size in the header and
/// keeps its aspect ratio; a favicon is the source for a whole square icon set the browser,
/// iOS and Android each pick from. Carrying that as an enum rather than the raw route segment
/// keeps the string <c>"favicon"</c> at the HTTP edge, where it arrives, instead of threading
/// it through the use case and into the image code.
/// </para>
/// <para>
/// The three logo kinds are processed identically — one rendition, aspect ratio preserved —
/// and differ only in which setting the admin panel saves the URL into and which directory the
/// files land in. The mark is deliberately <b>not</b> square-cropped: an operator who uploads a
/// wide image as a mark sees a wide mark in the preview, which is the mistake surfacing rather
/// than being hidden by a crop made on their behalf.
/// </para>
/// <para>
/// The route segment is the member name lower-cased (<c>logo</c>, <c>favicon</c>,
/// <c>logodark</c>, <c>logomark</c>); the storage directory is the same string.
/// </para>
/// </remarks>
public enum BrandingKind
{
    /// <summary>The header logo for light backgrounds. Aspect ratio preserved, one rendition.</summary>
    Logo = 0,

    /// <summary>The browser tab icon. Source for the full square icon set.</summary>
    Favicon = 1,

    /// <summary>The header logo for dark backgrounds. Aspect ratio preserved, one rendition.</summary>
    LogoDark = 2,

    /// <summary>The square icon-only logo for compact headers and sign-in pages. One rendition.</summary>
    LogoMark = 3,
}
