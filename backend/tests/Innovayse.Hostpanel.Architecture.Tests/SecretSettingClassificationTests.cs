using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.SDK.Plugins.Manifest;

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
    /// Every setting a shipped plugin manifest declares as secret, in <c>settings</c> or in the legacy <c>fields</c>,
    /// is in the registry too, and no plain setting is. A settings-only plugin is covered like a fields-only one.
    /// </summary>
    [Fact]
    public void ShippedPluginManifests_SecretSettings_AreInTheRegistry()
    {
        var manifests = Directory.GetFiles(SourceRoot(), "plugin.json", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        manifests.Should().NotBeEmpty();
        foreach (var path in manifests)
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path), JsonOptions)!;

            // Same union the runtime classifier reads: GetSettings() hides the legacy fields once settings exist.
            var settings = manifest.GetSettings().Concat(manifest.Fields.Select(PluginSetting.FromField)).ToList();

            settings.Should().NotBeEmpty("{0} declares no settings", path);
            foreach (var setting in settings)
            {
                SecretSettingKeys.IntegrationSecretFields.Contains(setting.Key).Should().Be(
                    setting.IsSecret(),
                    "{0} declares '{1}' as '{2}'",
                    path,
                    setting.Key,
                    setting.Type);
            }
        }
    }

    /// <summary>Finds <c>backend/src</c> by walking up from the test binaries.</summary>
    /// <returns>The absolute path of the source root.</returns>
    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Innovayse.SDK");
            if (Directory.Exists(candidate))
            {
                return Path.Combine(directory.FullName, "src");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("backend/src was not found above the test binaries.");
    }
}
