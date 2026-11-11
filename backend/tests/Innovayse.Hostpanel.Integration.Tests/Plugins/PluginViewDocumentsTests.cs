using System.Globalization;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>How a plugin's view documents are localised per language and kept, so a page request does not re-localise the view.</summary>
public sealed class PluginViewDocumentsTests
{
    /// <summary>The path the test plugin holds its one view at.</summary>
    private const string ViewPath = "views/report.json";

    /// <summary>The same language asked twice gets the same localised content, and the stored document is left as it was.</summary>
    [Fact]
    public void SameLanguageTwice_ReturnsEqualContent()
    {
        var views = Views();

        var first = InCulture("ru", () => views.Localised(ViewPath, PluginUiTexts.For(Manifest())));
        var second = InCulture("ru", () => views.Localised(ViewPath, PluginUiTexts.For(Manifest())));

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        Title(first!.Value).Should().Be("Отчёты");
        second!.Value.GetRawText().Should().Be(first.Value.GetRawText());
        Title(views.Find(ViewPath)!.Value).Should().Be("menu.reports");
    }

    /// <summary>A second language gets its own localisation, not the one made first for another language.</summary>
    [Fact]
    public void ASecondLanguage_GetsItsOwnLocalisation()
    {
        var views = Views();

        var russian = InCulture("ru", () => views.Localised(ViewPath, PluginUiTexts.For(Manifest())));
        var english = InCulture("en", () => views.Localised(ViewPath, PluginUiTexts.For(Manifest())));

        Title(russian!.Value).Should().Be("Отчёты");
        Title(english!.Value).Should().Be("Reports");
    }

    /// <summary>
    /// A language the platform does not support is resolved and cached as English: the texts carry <c>en</c>, which is
    /// the cache key, and the localised view is the English one, so a caller-chosen language adds no cache entry.
    /// </summary>
    [Fact]
    public void UnsupportedLanguage_IsLocalisedAndCachedAsEnglish()
    {
        var views = Views();

        var texts = InCulture("fr", () => PluginUiTexts.For(Manifest()));
        var french = views.Localised(ViewPath, texts);
        var english = InCulture("en", () => views.Localised(ViewPath, PluginUiTexts.For(Manifest())));

        texts.Language.Should().Be("en");
        Title(french!.Value).Should().Be("Reports");
        french.Value.GetRawText().Should().Be(english!.Value.GetRawText());
    }

    /// <summary>The language the texts are cached under is the language their labels are resolved in.</summary>
    [Fact]
    public void Texts_ResolveInTheLanguageTheyCarry()
    {
        var texts = InCulture("ru", () => PluginUiTexts.For(Manifest()));

        texts.Language.Should().Be("ru");

        // Resolved outside the culture on purpose: the texts use the language they captured, not the thread's culture.
        InCulture("en", () => texts.Resolve("menu.reports")).Should().Be("Отчёты");
    }

    /// <summary>A path the plugin does not hold answers null.</summary>
    [Fact]
    public void AnUnknownPath_IsNull()
    {
        var views = Views();

        InCulture("en", () => views.Localised("views/missing.json", PluginUiTexts.For(Manifest()))).Should().BeNull();
    }

    /// <summary>
    /// The labels nested inside a chat block (the assistant's <c>label</c>, the intro's <c>title</c> and <c>text</c>, and
    /// each suggestion's <c>label</c> and <c>text</c>) are localised like a block's own, because a label member is resolved
    /// at any depth; the routes beside them are left as written.
    /// </summary>
    [Fact]
    public void NestedChatLabels_AreLocalised()
    {
        const string chatPath = "views/chat.json";
        using var document = JsonDocument.Parse("""
            {"schema":1,"blocks":[{"kind":"chat","placeholder":"chat.placeholder",
              "send":{"method":"post","path":"/chat"},
              "assistant":{"label":"chat.assistant"},
              "intro":{"title":"chat.introTitle","text":"chat.introText"},
              "suggestions":[{"label":"chat.suggest.color","text":"chat.suggest.colorText"}]}]}
            """);
        var views = new PluginViewDocuments(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [chatPath] = document.RootElement.Clone() });
        var manifest = Manifest(
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string>
                {
                    ["chat.placeholder"] = "Ask",
                    ["chat.assistant"] = "Assistant",
                    ["chat.introTitle"] = "Hello",
                    ["chat.introText"] = "Describe it",
                    ["chat.suggest.color"] = "Colour",
                    ["chat.suggest.colorText"] = "Make it green",
                },
                ["ru"] = new Dictionary<string, string>
                {
                    ["chat.placeholder"] = "Спросите",
                    ["chat.assistant"] = "Помощник",
                    ["chat.introTitle"] = "Привет",
                    ["chat.introText"] = "Опишите",
                    ["chat.suggest.color"] = "Цвет",
                    ["chat.suggest.colorText"] = "Сделай зелёным",
                },
            });

        var chat = InCulture("ru", () => views.Localised(chatPath, PluginUiTexts.For(manifest)))!.Value
            .GetProperty("blocks")[0];

        chat.GetProperty("placeholder").GetString().Should().Be("Спросите");
        chat.GetProperty("assistant").GetProperty("label").GetString().Should().Be("Помощник");
        chat.GetProperty("intro").GetProperty("title").GetString().Should().Be("Привет");
        chat.GetProperty("intro").GetProperty("text").GetString().Should().Be("Опишите");
        chat.GetProperty("suggestions")[0].GetProperty("label").GetString().Should().Be("Цвет");
        chat.GetProperty("suggestions")[0].GetProperty("text").GetString().Should().Be("Сделай зелёным");
        chat.GetProperty("send").GetProperty("path").GetString().Should().Be("/chat");
    }

    /// <summary>Builds the documents of a plugin holding one view whose title is a locale key.</summary>
    /// <returns>The documents.</returns>
    private static PluginViewDocuments Views()
    {
        using var document = JsonDocument.Parse("""{"schema":1,"blocks":[{"kind":"heading","title":"menu.reports"}]}""");
        return new PluginViewDocuments(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [ViewPath] = document.RootElement.Clone() });
    }

    /// <summary>Builds a manifest whose locales translate the view's title into English and Russian.</summary>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest()
    {
        return Manifest(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["menu.reports"] = "Reports" },
            ["ru"] = new Dictionary<string, string> { ["menu.reports"] = "Отчёты" },
        });
    }

    /// <summary>Builds a manifest with the given locale texts.</summary>
    /// <param name="locales">The texts by locale, then by key.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales)
    {
        return new PluginManifest
        {
            Id = "views-plugin",
            Name = "Views plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Localises views.",
            Type = PluginType.App,
            Category = "tools",
            EntryPoint = "Views.Plugin",
            SdkVersion = "2.0.0",
            Locales = locales,
        };
    }

    /// <summary>Reads the title of the view's first block.</summary>
    /// <param name="view">A view document.</param>
    /// <returns>The title.</returns>
    private static string? Title(JsonElement view)
    {
        return view.GetProperty("blocks")[0].GetProperty("title").GetString();
    }

    /// <summary>Runs an action with the UI culture set, restoring the previous one afterwards.</summary>
    /// <typeparam name="T">The action's result.</typeparam>
    /// <param name="culture">The culture name to run in.</param>
    /// <param name="action">The action.</param>
    /// <returns>What the action returned.</returns>
    private static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
