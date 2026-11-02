using System.Globalization;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Turns a manifest <c>label</c> or <c>title</c> into the text for the request's language: a key of the manifest locales
/// in the request's language, else the same key in English, else the value itself as plain text.
/// </summary>
public static class PluginText
{
    /// <summary>The locale every manifest that uses a key must define it in, so it is the fallback.</summary>
    private const string FallbackLocale = "en";

    /// <summary>Resolves a manifest text for the current UI culture.</summary>
    /// <param name="manifest">The plugin's manifest, whose locales hold the keys.</param>
    /// <param name="text">The declared text: a key, plain text, or <see langword="null"/>.</param>
    /// <returns>The translated text, <paramref name="text"/> when it is not a key, or <see langword="null"/> for none.</returns>
    public static string? Resolve(PluginManifest manifest, string? text)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (text is null)
        {
            return null;
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        foreach (var locale in new[] { language, FallbackLocale })
        {
            if (manifest.Locales.TryGetValue(locale, out var texts) && texts.TryGetValue(text, out var translated))
            {
                return translated;
            }
        }

        return text;
    }
}
