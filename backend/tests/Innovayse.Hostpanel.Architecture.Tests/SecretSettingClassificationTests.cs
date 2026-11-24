using System.Text.Json;
using System.Text.Json.Serialization;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Holds <see cref="SecretSettingKeys"/> as the single source of which settings are secret: the
/// integration catalogue's password fields and every shipped plugin manifest's secret fields must
/// agree with it, so a credential cannot be rendered as a password box yet returned by a settings
/// read, or the other way round.
/// </summary>
public sealed class SecretSettingClassificationTests
{
    /// <summary>JSON options matching how the host reads <c>plugin.json</c>.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>A catalogue field is a password field exactly when the registry classifies it as secret.</summary>
    [Fact]
    public void Catalogue_PasswordFields_MatchTheRegistry()
    {
        foreach (var (slug, entry) in IntegrationCatalogue.All)
        {
            foreach (var field in entry.Fields)
            {
                var key = IntegrationSettingKeys.FieldKey(slug, field.Key);
                SecretSettingKeys.IsSecret(key).Should().Be(
                    SecretSettingKeys.IsSecretFieldType(field.Type),
                    "{0} is typed '{1}' in the catalogue",
                    key,
                    field.Type);
            }
        }
    }

    /// <summary>
    /// Every setting a plugin manifest declares as secret, in <c>settings</c> or in the legacy <c>fields</c>, is in the
    /// registry too, and no plain setting is. A settings-only plugin is covered like a fields-only one. The host ships
    /// no plugin, so the manifests are written here, in the two shapes a plugin may use.
    /// </summary>
    /// <param name="listProperty">The manifest property that lists the fields: the legacy <c>fields</c> or the newer <c>settings</c>.</param>
    [Theory]
    [InlineData("fields")]
    [InlineData("settings")]
    public void PluginManifests_SecretSettings_AreInTheRegistry(string listProperty)
    {
        var manifestJson = $$"""
            {
              "id": "example", "name": "Example", "version": "1.0.0", "author": "Example", "description": "Example",
              "type": "provisioning", "category": "Hosting", "entryPoint": "Example.Plugin", "sdkVersion": "3.0",
              "{{listProperty}}": [
                { "key": "host", "label": "Host", "type": "text" },
                { "key": "api_key", "label": "Key", "type": "secret" }
              ]
            }
            """;
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, JsonOptions)!;

        // Same union the runtime classifier reads: GetSettings() hides the legacy fields once settings exist.
        var settings = manifest.GetSettings().Concat(manifest.Fields.Select(PluginSetting.FromField)).ToList();

        settings.Should().NotBeEmpty();
        foreach (var setting in settings)
        {
            SecretSettingKeys.IntegrationSecretFields.Contains(setting.Key).Should().Be(
                setting.IsSecret(),
                "'{0}' is declared as '{1}'",
                setting.Key,
                setting.Type);
        }
    }
}
