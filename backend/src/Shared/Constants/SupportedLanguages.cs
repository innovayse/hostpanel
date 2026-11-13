namespace Innovayse.Hostpanel.Shared.Constants;

/// <summary>
///     Supported content/UI languages across the whole platform.
///     Single source of truth: every service, cache key and culture provider
///     must resolve languages through this class.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    ///     Default fallback language when no valid X-Language header is present.
    /// </summary>
    public const string Default = "en";

    /// <summary>
    ///     All supported language codes: English, Russian, Armenian, Spanish.
    /// </summary>
    public static readonly HashSet<string> All = ["en", "ru", "hy", "es"];

    /// <summary>
    ///     Checks whether the given language code is supported.
    /// </summary>
    /// <param name="language">ISO 639-1 language code (e.g. "en", "ru", "hy").</param>
    /// <returns><c>true</c> if the language is supported; otherwise <c>false</c>.</returns>
    public static bool IsSupported(string? language) =>
        language != null && All.Contains(language);
}
