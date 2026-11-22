using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="RegistrarConnectionBuilder"/>: the connection is read from the plugin's own settings by their
/// <c>connectionRole</c>, so the host needs to know no setting key.
/// </summary>
public sealed class RegistrarConnectionBuilderTests
{
    /// <summary>The id of the plugin every test builds the connection for.</summary>
    private const string Id = "role-registrar";

    /// <summary>Builds the configuration the activator would compose from the given values.</summary>
    /// <param name="values">Setting key to value.</param>
    /// <returns>A configuration keyed by the full setting paths.</returns>
    private static IConfiguration Config(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => IntegrationSettingKeys.FieldKey(Id, v.Key), v => v.Value))
            .Build();
    }

    /// <summary>The manifest of a registrar whose settings carry the roles.</summary>
    /// <param name="settings">The settings the manifest lists.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(params PluginSetting[] settings)
    {
        var loaded = RegistrarRegistryTestSeeds.Plugin(Id, PluginType.Registrar, typeof(ScriptedRegistrarPlugin));

        return new PluginManifest
        {
            Id = Id,
            Name = loaded.Manifest.Name,
            Version = loaded.Manifest.Version,
            Author = loaded.Manifest.Author,
            Description = loaded.Manifest.Description,
            Type = PluginType.Registrar,
            Category = loaded.Manifest.Category,
            EntryPoint = loaded.Manifest.EntryPoint,
            SdkVersion = loaded.Manifest.SdkVersion,
            ManifestVersion = 2,
            Settings = settings,
        };
    }

    /// <summary>Every role is read from the setting that plays it, whatever the setting is called.</summary>
    [Fact]
    public void Build_ReadsEachRoleFromItsSetting()
    {
        var manifest = Manifest(
            new PluginSetting { Key = "who", Label = "Who", Type = PluginSettingType.Text, ConnectionRole = RegistrarConnectionRole.AccountName },
            new PluginSetting { Key = "token", Label = "Token", Type = PluginSettingType.Secret, ConnectionRole = RegistrarConnectionRole.ApiKey },
            new PluginSetting { Key = "endpoint", Label = "Endpoint", Type = PluginSettingType.Url, ConnectionRole = RegistrarConnectionRole.ApiUrl },
            new PluginSetting { Key = "ip", Label = "IP", Type = PluginSettingType.Text, ConnectionRole = RegistrarConnectionRole.ClientIp },
            new PluginSetting { Key = "sandbox", Label = "Sandbox", Type = PluginSettingType.Boolean, ConnectionRole = RegistrarConnectionRole.TestMode });

        var connection = RegistrarConnectionBuilder.Build(manifest, Config(new()
        {
            ["who"] = " account ",
            ["token"] = "key",
            ["endpoint"] = "https://api.example.test",
            ["ip"] = "10.0.0.1",
            ["sandbox"] = "true",
        }));

        connection.Should().Be(new RegistrarConnection("account", "key", "https://api.example.test", "10.0.0.1", true));
    }

    /// <summary>Test mode is on only for the enabled value, in any letter case, and off for anything else or nothing.</summary>
    /// <param name="value">The stored value.</param>
    /// <param name="expected">Whether test mode is on.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("maybe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Build_TestMode_IsOnOnlyForTheEnabledValue(string? value, bool expected)
    {
        var manifest = Manifest(RegistrarRegistryTestSeeds.Settings());

        var connection = RegistrarConnectionBuilder.Build(manifest, Config(new() { [RegistrarRegistryTestSeeds.TestModeField] = value }));

        connection.TestMode.Should().Be(expected);
    }

    /// <summary>A part with no setting, or whose setting holds nothing, is null, and a manifest with no roles gives an empty connection.</summary>
    [Fact]
    public void Build_MissingRolesAndEmptyValues_AreNull()
    {
        var withRoles = RegistrarConnectionBuilder.Build(
            Manifest(RegistrarRegistryTestSeeds.Settings()),
            Config(new() { [RegistrarRegistryTestSeeds.AccountField] = "  ", [RegistrarRegistryTestSeeds.KeyField] = "key" }));
        var withoutRoles = RegistrarConnectionBuilder.Build(
            Manifest(new PluginSetting { Key = "plain", Label = "Plain", Type = PluginSettingType.Text }),
            Config(new() { ["plain"] = "value" }));

        withRoles.Should().Be(new RegistrarConnection(null, "key", null, null, false));
        withoutRoles.Should().Be(new RegistrarConnection(null, null, null, null, false));
    }
}
