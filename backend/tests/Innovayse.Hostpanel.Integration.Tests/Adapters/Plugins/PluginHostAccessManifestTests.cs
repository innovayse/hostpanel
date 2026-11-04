using System.Globalization;
using System.Resources;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rule for <c>hostAccess</c> (#758): it names only kinds the SDK defines (<c>ssh</c>), with its own code and
/// a sentence in en, ru and hy. The #818 capability rules are tested by drafts-818. No container or database.
/// </summary>
public sealed class PluginHostAccessManifestTests
{
    /// <summary>The cultures every refusal must have a translated sentence in, besides English.</summary>
    private static readonly string[] TranslatedCultures = ["ru", "hy"];

    /// <summary>Reads a version 2 manifest of a type, with the given members after the required ones.</summary>
    /// <param name="type">The plugin type the manifest declares, by its JSON name.</param>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string type, string extra)
    {
        var json = $$"""
            {
              "id": "capability-plugin",
              "name": "Capability Plugin",
              "version": "1.0.0",
              "author": "Test",
              "description": "A plugin used only in tests.",
              "type": "{{type}}",
              "category": "Test",
              "entryPoint": "Test.Plugin",
              "sdkVersion": "2.1",
              "manifestVersion": 2{{extra}}
            }
            """;
        return PluginManifestReader.Deserialize(json)!;
    }

    /// <summary>Asserts that a manifest breaks exactly one given rule.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="rule">The rule it must break.</param>
    private static void AssertBreaks(PluginManifest manifest, PluginManifestRule rule)
    {
        PluginManifestValidator.Validate(manifest).Should().NotBeNull().And.Match<PluginManifestViolation>(v => v.Rule == rule);
    }

    /// <summary><c>hostAccess</c> may name <c>ssh</c>, and an empty or absent list is no request at all (#758).</summary>
    [Fact]
    public void Validate_KnownHostAccess_Passes()
    {
        PluginManifestValidator.Validate(Manifest("App", ""","hostAccess":["ssh"]""")).Should().BeNull();
        PluginManifestValidator.Validate(Manifest("Fulfilment", ""","capabilities":["endpoints"],"hostAccess":["ssh"]""")).Should().BeNull();
        PluginManifestValidator.Validate(Manifest("App", ""","hostAccess":[]""")).Should().BeNull();
        PluginManifestValidator.Validate(Manifest("App", ""","hostAccess":null""")).Should().BeNull();
    }

    /// <summary>A kind of hosting access the SDK does not define is refused, whatever its spelling (#758).</summary>
    /// <param name="kind">The declared kind.</param>
    [Theory]
    [InlineData("SSH")]
    [InlineData("ftp")]
    [InlineData("root")]
    [InlineData("")]
    public void Validate_UnknownHostAccess_BreaksHostAccessUnknown(string kind)
    {
        AssertBreaks(Manifest("App", $$""","hostAccess":["{{kind}}"]"""), PluginManifestRule.HostAccessUnknown);
    }

    /// <summary>The host access rule answers with its own code, the constant the API answers with.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="code">The code it answers with.</param>
    [Theory]
    [InlineData(PluginManifestRule.HostAccessUnknown, HostpanelErrorCodes.PLUGIN_HOST_ACCESS_UNKNOWN)]
    public void HostAccessRule_HasItsOwnCode(PluginManifestRule rule, string code)
    {
        PluginPackageErrors.CodeFor(rule).Should().Be(code);
    }

    /// <summary>
    /// The host access code has an English sentence and its own Russian and Armenian one, so a refused install is answered in
    /// the caller's language and never with the code itself.
    /// </summary>
    /// <param name="code">The code.</param>
    [Theory]
    [InlineData(HostpanelErrorCodes.PLUGIN_HOST_ACCESS_UNKNOWN)]
    public void HostAccessCode_HasASentenceInEveryLanguage(string code)
    {
        var manager = new ResourceManager(typeof(ErrorMessages));
        var english = manager.GetString(code, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace().And.NotBe(code);
        foreach (var culture in TranslatedCultures)
        {
            var translated = manager.GetString(code, CultureInfo.GetCultureInfo(culture));
            translated.Should().NotBeNullOrWhiteSpace().And.NotBe(code).And.NotBe(english, "the {0} sentence is a translation", culture);
        }
    }
}
