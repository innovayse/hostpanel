using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>
/// The two languages a knowledge base read picks a translation by: the one the reader asked for, and the one to
/// fall back on when the article is not written in it.
/// </summary>
public static class KbLanguage
{
    /// <summary>The language the current request asked for.</summary>
    /// <returns>The two letter code of the request's UI culture, for example <c>ru</c>.</returns>
    public static string Requested() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>
    /// The language to fall back on: the configured default locale reduced to its language (so <c>en-US</c> is
    /// <c>en</c>), or the platform default when that is not one of the supported languages.
    /// </summary>
    /// <param name="options">The locale settings of this deployment.</param>
    /// <returns>A supported language code.</returns>
    public static string Fallback(LocaleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.DefaultLocale;
        var language = configured.Length >= 2 ? configured[..2].ToLowerInvariant() : configured;

        return SupportedLanguages.IsSupported(language) ? language : SupportedLanguages.Default;
    }
}
