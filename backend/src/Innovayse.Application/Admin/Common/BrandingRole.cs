namespace Innovayse.Application.Admin.Common;

/// <summary>
/// What a generated branding file is linked as by the storefront.
/// </summary>
/// <remarks>
/// The storefront needs to emit a different tag for each of these -- <c>rel="icon"</c> with a
/// <c>sizes</c> attribute, <c>rel="apple-touch-icon"</c>, or an entry in the web app manifest --
/// so the purpose travels with the file rather than being re-derived from its name by a regular
/// expression on the other side of the API.
/// </remarks>
public enum BrandingRole
{
    /// <summary>The image as uploaded, re-encoded. The header logo, or the favicon's own source.</summary>
    Primary = 0,

    /// <summary>A <c>rel="icon"</c> PNG at the size named by the rendition.</summary>
    Icon = 1,

    /// <summary>The iOS home-screen icon, linked as <c>rel="apple-touch-icon"</c>.</summary>
    AppleTouch = 2,

    /// <summary>An Android/PWA icon, listed in the web app manifest.</summary>
    Android = 3,
}
