using System.Collections.Concurrent;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The view documents of one plugin, read and checked against the view schema when the plugin was loaded, by the
/// package-relative path its pages and slots name them with. Held in memory for as long as the plugin is loaded, so a
/// panel request never reads the package from disk.
/// </summary>
public sealed class PluginViewDocuments
{
    /// <summary>The documents by package-relative path, compared exactly.</summary>
    private readonly IReadOnlyDictionary<string, JsonElement> _documents;

    /// <summary>
    /// The localised documents already made, by path and language. Bounded by construction: only paths this plugin holds
    /// and only the platform's supported languages are ever added, and the instance lives exactly as long as the plugin
    /// is loaded, so a page request does not re-localise the whole view every time.
    /// </summary>
    private readonly ConcurrentDictionary<LocalisedViewKey, JsonElement> _localised = new();

    /// <summary>Initializes a new instance of the <see cref="PluginViewDocuments"/> class.</summary>
    /// <param name="documents">The documents by package-relative path; each element must outlive its <see cref="JsonDocument"/> (a clone).</param>
    public PluginViewDocuments(IReadOnlyDictionary<string, JsonElement> documents)
    {
        _documents = documents;
    }

    /// <summary>Gets the documents of a plugin that names none: a manifest without pages or slots.</summary>
    public static PluginViewDocuments None { get; } = new(new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    /// <summary>Gets the package-relative paths held.</summary>
    public IEnumerable<string> Paths => _documents.Keys;

    /// <summary>Finds a document by the path a page or slot names.</summary>
    /// <param name="path">The package-relative path, as the manifest writes it.</param>
    /// <returns>The document, or <see langword="null"/> when the plugin holds none at that path.</returns>
    public JsonElement? Find(string path)
    {
        return _documents.TryGetValue(path, out var document) ? document : null;
    }

    /// <summary>
    /// Finds a document by the path a page or slot names, with every label resolved in the texts' language. Each
    /// document is localised once per language and then served from memory. The cache is bounded by the paths this
    /// plugin holds times <see cref="SupportedLanguages.All"/>: an unknown path never reaches it, and
    /// <see cref="PluginUiTexts.Language"/> is always a supported language (<see cref="PluginUiTexts.For"/> normalises
    /// any other to <see cref="SupportedLanguages.Default"/>), so a caller cannot grow it.
    /// </summary>
    /// <param name="path">The package-relative path, as the manifest writes it.</param>
    /// <param name="texts">The plugin's texts in the request's language.</param>
    /// <returns>The localised document, or <see langword="null"/> when the plugin holds none at that path.</returns>
    public JsonElement? Localised(string path, PluginUiTexts texts)
    {
        // An unknown path is answered before the cache is touched, so it can never add an entry.
        if (!_documents.TryGetValue(path, out var document))
        {
            return null;
        }

        // The value factory may run twice under a race; both results are equal, and only one is kept.
        return _localised.GetOrAdd(new LocalisedViewKey(path, texts.Language), _ => texts.Localise(document));
    }
}
