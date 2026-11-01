using System.Text.Json;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// How a manifest answers its settings: <c>GetSettings()</c> over the legacy <c>fields</c> and the version 2
/// <c>settings</c>, and how the host refuses a setting whose type it does not define.
/// </summary>
public sealed class PluginManifestSettingsTests
{
    /// <summary>Builds a manifest text with the given extra members after the required ones.</summary>
    /// <param name="extra">Extra JSON members, for example <c>,"settings":[]</c>, or an empty string.</param>
    /// <returns>The manifest text.</returns>
    private static string Manifest(string extra)
    {
        return "{\"id\":\"p\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + "\"type\":\"payment\",\"category\":\"Payment\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"1.0\"" + extra + "}";
    }

    /// <summary>Legacy fields map to settings of the matching kind, keeping key, label and requirement.</summary>
    [Fact]
    public void GetSettings_FieldsOnly_MapsEachFieldToASetting()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"fields":[
              {"key":"a","label":"A","type":"text","required":true},
              {"key":"b","label":"B","type":"secret"},
              {"key":"c","label":"C","type":"password"},
              {"key":"d","label":"D","type":"select"},
              {"key":"e","label":"E","type":"textarea"},
              {"key":"f","label":"F","type":"somethingElse"}
            ]
            """));

        var settings = manifest!.GetSettings();

        settings.Select(s => s.Key).Should().Equal("a", "b", "c", "d", "e", "f");
        settings.Select(s => s.Type).Should().Equal(
            PluginSettingType.Text, PluginSettingType.Secret, PluginSettingType.Secret,
            PluginSettingType.Select, PluginSettingType.Textarea, PluginSettingType.Text);
        settings[0].Label.Should().Be("A");
        settings[0].Required.Should().BeTrue();
        settings[1].Required.Should().BeFalse();
    }

    /// <summary>When a manifest lists settings, they win over the legacy fields.</summary>
    [Fact]
    public void GetSettings_SettingsAndFields_SettingsSupersede()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"fields":[{"key":"old","label":"Old","type":"text"}],
            "settings":[{"key":"new","label":"New","type":"url"}]
            """));

        manifest!.GetSettings().Select(s => s.Key).Should().Equal("new");
        manifest.Fields.Select(f => f.Key).Should().Equal("old");
    }

    /// <summary>A setting type name the host does not define refuses the manifest instead of reading as text.</summary>
    [Fact]
    public void Deserialize_UnknownSettingTypeName_Throws()
    {
        var act = () => PluginManifestReader.Deserialize(Manifest(
            ",\"settings\":[{\"key\":\"k\",\"label\":\"K\",\"type\":\"apiToken\"}]"));

        act.Should().Throw<JsonException>();
    }

    /// <summary>A numeric setting type outside the defined kinds, which the string converter accepts, is refused too.</summary>
    /// <param name="type">The numeric type written in the manifest.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    public void Deserialize_UndefinedNumericSettingType_Throws(string type)
    {
        var act = () => PluginManifestReader.Deserialize(Manifest(
            ",\"settings\":[{\"key\":\"k\",\"label\":\"K\",\"type\":" + type + "}]"));

        act.Should().Throw<JsonException>().WithMessage("*'k'*");
    }

    /// <summary>A setting without a type is refused, never defaulted to a plain kind.</summary>
    [Fact]
    public void Deserialize_SettingWithoutType_Throws()
    {
        var act = () => PluginManifestReader.Deserialize(Manifest(
            ",\"settings\":[{\"key\":\"k\",\"label\":\"K\"}]"));

        act.Should().Throw<JsonException>();
    }

    /// <summary>The file reader leaves a plugin with an unknown setting type out: it does not load.</summary>
    [Fact]
    public void TryRead_UnknownSettingType_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Manifest(",\"settings\":[{\"key\":\"k\",\"label\":\"K\",\"type\":\"apiToken\"}]"));
        try
        {
            var manifest = new PluginManifestReader(NullLogger<PluginManifestReader>.Instance).TryRead(path);

            manifest.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
