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
    /// Two-letter locale code (<c>en</c>, <c>ru</c>, <c>hy</c>) used when a requested locale has no
    /// translation. English is the default because every seeded translation carries it.
    /// </summary>
    public string DefaultLocale { get; set; } = "en";
}
