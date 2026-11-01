using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Extensions;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <see cref="SettingSecrecy"/>: which setting keys are secret. A secret is the union of the registry keys, the
/// manifest settings and the legacy manifest fields, and a kind the host does not define fails closed.
/// </summary>
public sealed class SettingSecrecyTests
{
    /// <summary>Builds a loaded plugin with the given settings and legacy fields.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="settings">The v2 settings, or none.</param>
    /// <param name="fields">The legacy fields, or none.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, PluginSetting[]? settings = null, PluginField[]? fields = null)
    {
        return new LoadedPlugin(
            new PluginManifest
            {
                Id = id,
                Name = "Test plugin",
                Version = "1.0.0",
                Author = "Tests",
                Description = "Plugin used by the secrecy tests.",
                Type = PluginType.Payment,
                Category = "Payment",
                EntryPoint = typeof(object).FullName!,
                SdkVersion = "1.0",
                Settings = settings ?? [],
                Fields = fields ?? [],
            },
            typeof(object));
    }

    /// <summary>Builds a registry holding <paramref name="plugins"/>.</summary>
    /// <param name="plugins">The loaded plugins.</param>
    /// <returns>The registry.</returns>
    private static PluginRegistry Registry(params LoadedPlugin[] plugins)
    {
        var registry = new PluginRegistry();
        foreach (var plugin in plugins)
        {
            registry.Register(plugin);
        }

        return registry;
    }

    /// <summary>A secret setting of a version 2 manifest is secret, like a legacy secret field of another plugin.</summary>
    [Fact]
    public void SecretSettingAndSecretField_BothAreSecret()
    {
        var secrecy = SettingSecrecy.From(Registry(
            Plugin(
                "p",
                settings:
                [
                    new PluginSetting { Key = "apiKey", Label = "K", Type = PluginSettingType.Secret },
                    new PluginSetting { Key = "host", Label = "H", Type = PluginSettingType.Url },
                ]),
            Plugin("q", fields: [new PluginField { Key = "token", Label = "T", Type = "secret" }])));

        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("p", "apiKey")).Should().BeTrue();
        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("p", "host")).Should().BeFalse();
        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("q", "token")).Should().BeTrue();
    }

    /// <summary>A secret legacy field stays secret in a mixed manifest whose settings omit it, and its value is masked.</summary>
    [Fact]
    public void MixedManifest_SettingsOmitASecretField_StillMasksIt()
    {
        var secrecy = SettingSecrecy.From(Registry(Plugin(
            "mixed",
            settings: [new PluginSetting { Key = "host", Label = "H", Type = PluginSettingType.Url }],
            fields:
            [
                new PluginField { Key = "token", Label = "T", Type = "secret" },
                new PluginField { Key = "legacyPass", Label = "P", Type = "password" },
            ])));

        var token = Setting.Create(IntegrationSettingKeys.FieldKey("mixed", "token"), "clear-token-value", null);
        var dto = token.ToDto(secrecy);

        dto.IsSecret.Should().BeTrue();
        dto.HasValue.Should().BeTrue();
        dto.Value.Should().BeNull();
        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("mixed", "legacyPass")).Should().BeTrue();
        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("mixed", "host")).Should().BeFalse();
    }

    /// <summary>A setting kind the host does not define is secret: it never reads as plain text.</summary>
    [Fact]
    public void UndefinedSettingKind_IsSecret()
    {
        var secrecy = SettingSecrecy.From(Registry(Plugin(
            "odd",
            settings: [new PluginSetting { Key = "mystery", Label = "M", Type = (PluginSettingType)99 }])));

        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("odd", "mystery")).Should().BeTrue();
    }

    /// <summary>Plugin keys are compared without regard to case, as the settings table keys are.</summary>
    [Fact]
    public void PluginSecretKey_IsMatchedIgnoringCase()
    {
        var secrecy = SettingSecrecy.From(Registry(Plugin(
            "p",
            settings: [new PluginSetting { Key = "apiKey", Label = "K", Type = PluginSettingType.Secret }])));

        secrecy.IsSecret(IntegrationSettingKeys.FieldKey("p", "APIKEY")).Should().BeTrue();
    }
}
