using System.Globalization;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>How a manifest label or title is resolved for the request's language, with English as the fallback.</summary>
public sealed class PluginTextTests
{
    /// <summary>A locale code written in another case is still found, both for the request's language and for English.</summary>
    [Fact]
    public void LocaleCode_IsComparedIgnoringCase()
    {
        var manifest = new PluginManifest
        {
            Id = "case-plugin",
            Name = "Case plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Resolves texts.",
            Type = PluginType.App,
            Category = "tools",
            EntryPoint = "Case.Plugin",
            SdkVersion = "2.0.0",
            Locales = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["EN"] = new Dictionary<string, string> { ["menu.reports"] = "Reports", ["menu.only"] = "Only English" },
                ["Ru"] = new Dictionary<string, string> { ["menu.reports"] = "Отчёты" },
            },
        };

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ru");
            PluginText.Resolve(manifest, "menu.reports").Should().Be("Отчёты");
            PluginText.Resolve(manifest, "menu.only").Should().Be("Only English");
            PluginText.Resolve(manifest, "Plain text").Should().Be("Plain text");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// The overload that takes a language looks the key up in that language whatever the thread's culture, falls back to
    /// English for a key it lacks, and passes plain text and null through.
    /// </summary>
    [Fact]
    public void Resolve_WithALanguage_IgnoresTheCurrentCulture()
    {
        var manifest = new PluginManifest
        {
            Id = "language-plugin",
            Name = "Language plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Resolves texts.",
            Type = PluginType.App,
            Category = "tools",
            EntryPoint = "Language.Plugin",
            SdkVersion = "2.0.0",
            Locales = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string> { ["menu.reports"] = "Reports", ["menu.only"] = "Only English" },
                ["ru"] = new Dictionary<string, string> { ["menu.reports"] = "Отчёты" },
            },
        };

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            PluginText.Resolve(manifest, "menu.reports", "ru").Should().Be("Отчёты");
            PluginText.Resolve(manifest, "menu.only", "ru").Should().Be("Only English");
            PluginText.Resolve(manifest, "Plain text", "ru").Should().Be("Plain text");
            PluginText.Resolve(manifest, null, "ru").Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
