namespace Innovayse.Application.Admin.Common;

/// <summary>
/// Which storefront branding image an upload is for.
/// </summary>
/// <remarks>
/// The two differ in more than their label. A logo is rendered at one size in the header and
/// keeps its aspect ratio; a favicon is the source for a whole square icon set the browser,
/// iOS and Android each pick from. Carrying that as an enum rather than the raw route segment
/// keeps the string <c>"favicon"</c> at the HTTP edge, where it arrives, instead of threading
/// it through the use case and into the image code.
/// </remarks>
public enum BrandingKind
{
    /// <summary>The storefront header logo. Aspect ratio preserved, one rendition.</summary>
    Logo = 0,

    /// <summary>The browser tab icon. Source for the full square icon set.</summary>
    Favicon = 1,
}
