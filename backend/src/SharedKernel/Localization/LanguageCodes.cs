namespace Innovayse.Hostpanel.SharedKernel.Localization;

/// <summary>
/// The language codes the platform answers in, held where the domain can reach them: the one list, in a fixed
/// order, that <c>SupportedLanguages</c> (the shared constants every service reads) and every per-language
/// storage rule are built from. A language is added here and nowhere else.
/// </summary>
public static class LanguageCodes
{
    /// <summary>The language every record must have and every missing translation falls back to.</summary>
    public const string Default = "en";

    /// <summary>Every language code, the default first: English, Russian, Armenian, German, Spanish, French.</summary>
    public static readonly IReadOnlyList<string> All = [Default, "ru", "hy", "de", "es", "fr"];

    /// <summary>Every language code but the default, in the order of <see cref="All"/>: the languages that have a row of their own beside the default one.</summary>
    public static readonly IReadOnlyList<string> NonDefault = [.. All.Where(code => code != Default)];
}
