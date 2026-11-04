using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Ui;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// One plugin's texts in the request's language (<c>X-Language</c>, the current UI culture, English when the platform
/// does not support it), falling back to English key by key: the panels receive finished labels and never translate a
/// plugin key themselves.
/// </summary>
public sealed class PluginUiTexts
{
    /// <summary>The view members holding labels, indexed.</summary>
    private static readonly FrozenSet<string> LabelProperties = PluginViews.LabelProperties.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The plugin manifest whose locales hold the texts.</summary>
    private readonly PluginManifest _manifest;

    /// <summary>Initializes a new instance of the <see cref="PluginUiTexts"/> class.</summary>
    /// <param name="manifest">The plugin manifest whose locales hold the texts.</param>
    /// <param name="language">The lower-cased two-letter supported language the texts are picked for.</param>
    private PluginUiTexts(PluginManifest manifest, string language)
    {
        _manifest = manifest;
        Language = language;
    }

    /// <summary>
    /// Gets the lower-cased two-letter ISO 639-1 code the texts are resolved in, always one of
    /// <see cref="SupportedLanguages.All"/> (<c>en</c>, <c>ru</c>, <c>hy</c>). It is both the language every label is looked
    /// up in and the key a cache of localised views is kept under, so the two can never disagree.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Picks a plugin's texts for the request's language: the current UI culture's two-letter language when the platform
    /// supports it, otherwise <see cref="SupportedLanguages.Default"/>.
    /// </summary>
    /// <param name="manifest">The plugin manifest, whose <see cref="PluginManifest.Locales"/> hold the texts.</param>
    /// <returns>The texts.</returns>
    public static PluginUiTexts For(PluginManifest manifest)
    {
        var requested = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

        // Normalised here, once, so an unsupported language (a culture set by something other than X-Language, which
        // only ever sets a supported one) is answered in English and cached under English: no caller can add a cache
        // entry for a language of its choosing, and the labels are never resolved in a language the cache does not name.
        var language = SupportedLanguages.IsSupported(requested) ? requested : SupportedLanguages.Default;
        return new PluginUiTexts(manifest, language);
    }

    /// <summary>
    /// The text of a label: <see cref="Language"/>, else English, else the value itself, which is then plain text (the
    /// manifest validator refuses at load a key-shaped label that English lacks). One rule for menus, pages, slots and
    /// views: it is <see cref="PluginText.Resolve(PluginManifest, string?, string)"/>.
    /// </summary>
    /// <param name="label">A key of the manifest locales, or plain text.</param>
    /// <returns>The text to show.</returns>
    public string Resolve(string label)
    {
        return PluginText.Resolve(_manifest, label, Language);
    }

    /// <summary>A copy of a view document with every string held by a label member (<see cref="PluginViews.LabelProperties"/>), at any depth, resolved.</summary>
    /// <param name="view">The view document as the package holds it.</param>
    /// <returns>The localised copy; the original is left as it is.</returns>
    public JsonElement Localise(JsonElement view)
    {
        var node = JsonNode.Parse(view.GetRawText());
        Replace(node);
        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>Resolves the labels under <paramref name="node"/>, at any depth, in place.</summary>
    /// <param name="node">The node to walk.</param>
    private void Replace(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray items:
                foreach (var item in items)
                {
                    Replace(item);
                }

                break;
            case JsonObject members:
                // Copied first: assigning a member while enumerating the object is not allowed.
                foreach (var (name, value) in members.ToList())
                {
                    if (LabelProperties.Contains(name) && value is JsonValue text && text.TryGetValue<string>(out var label))
                    {
                        members[name] = Resolve(label);
                    }
                    else
                    {
                        Replace(value);
                    }
                }

                break;
        }
    }
}
