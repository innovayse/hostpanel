using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Turns a manifest <c>label</c> or <c>title</c> into the text for a language: a key of the manifest locales in that
/// language, else the same key in English, else the value itself as plain text.
/// </summary>
public static class PluginText
{
    /// <summary>The locale every manifest that uses a key must define it in, so it is the fallback.</summary>
    private const string FallbackLocale = "en";

    /// <summary>Resolves a manifest text for the current UI culture (the request's <c>X-Language</c>).</summary>
    /// <param name="manifest">The plugin's manifest, whose locales hold the keys.</param>
    /// <param name="text">The declared text: a key, plain text, or <see langword="null"/>.</param>
    /// <returns>The translated text, <paramref name="text"/> when it is not a key, or <see langword="null"/> for none.</returns>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Resolve(PluginManifest manifest, string? text)
    {
        return Resolve(manifest, text, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    /// <summary>Resolves a manifest text for a language the caller has already chosen.</summary>
    /// <param name="manifest">The plugin's manifest, whose locales hold the keys.</param>
    /// <param name="text">The declared text: a key, plain text, or <see langword="null"/>.</param>
    /// <param name="language">The two-letter language code to look the key up in first, compared ignoring case.</param>
    /// <returns>The translated text, <paramref name="text"/> when it is not a key, or <see langword="null"/> for none.</returns>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Resolve(PluginManifest manifest, string? text, string language)
    {
        if (text is null)
        {
            return null;
        }

        foreach (var locale in new[] { language, FallbackLocale })
        {
            // A locale code is compared ignoring case, as the manifest validator compares it, so a manifest that
            // declares "EN" or "Ru" is read the same as one that declares "en" or "ru".
            var texts = manifest.Locales
                .FirstOrDefault(pair => string.Equals(pair.Key, locale, StringComparison.OrdinalIgnoreCase)).Value;
            if (texts is not null && texts.TryGetValue(text, out var translated))
            {
                return translated;
            }
        }

        return text;
    }
}
