namespace Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

/// <summary>
/// Where a plugin folder comes from. It decides whether the signature check applies: the host trusts its own files,
/// not what an operator uploaded. Values start at 1; 0 is not an origin.
/// </summary>
public enum PluginOrigin
{
    /// <summary>
    /// Shipped inside the host image, staged from the solution at build time (the payment provider is one). These
    /// files are part of the host, so they are exactly as trustworthy as the image they came in and carry no
    /// signature of their own.
    /// </summary>
    BuiltIn = 1,

    /// <summary>Uploaded by an administrator into the upload folder, or placed there by the operator. Authenticity has to be proven by a signature.</summary>
    Uploaded = 2,
}
