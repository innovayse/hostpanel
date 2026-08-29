namespace Innovayse.Application.Common.Options;

/// <summary>
/// The language the panel falls back to when content has no translation for the one requested.
/// </summary>
/// <remarks>
/// Not a section: <c>DefaultLocale</c> is a single bare top-level key, so the class names the key
/// it is built from rather than a section it does not have.
/// </remarks>
public sealed class LocaleOptions
{
    /// <summary>The configuration key this value is read from. Not a section -- a bare top-level key.</summary>
    public const string ConfigurationKey = "DefaultLocale";

    /// <summary>
    /// The languages this product answers in, most specific first. Used by the API to build the
    /// request-localisation culture list, and it is the same set the portal ships under
    /// <c>client/locales/</c> -- adding a fourth language means a new folder there, a new
    /// <c>ValidationMessages.&lt;code&gt;.resx</c> beside the neutral one, and this list.
    /// </summary>
    /// <remarks>
    /// Kept here rather than in the API's composition root because the resource files it lines up
    /// with live in this layer, and a supported-language list that drifts from the resources it
    /// describes fails silently: an unlisted language is simply served English.
    /// </remarks>
    public static readonly IReadOnlyList<string> SupportedLocales = ["en", "ru", "hy"];

    /// <summary>
    /// Two-letter locale code (<c>en</c>, <c>ru</c>, <c>hy</c>) used when a requested locale has no
    /// translation. English is the default because every seeded translation carries it.
    /// </summary>
    public string DefaultLocale { get; set; } = "en";
}
