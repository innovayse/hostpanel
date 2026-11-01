using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginActivator"/>'s required check: it reads the manifest's settings through
/// <c>GetSettings()</c>, so a required setting of a version 2 manifest gates the plugin, a legacy required field
/// does the same when the manifest has no settings, and the settings win when a manifest lists both.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginActivatorRequiredSettingTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The key of the setting or field the manifests below mark required.</summary>
    private const string RequiredKey = "endpoint";

    /// <summary>Builds a payment plugin whose manifest declares the given settings and legacy fields.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="settings">The version 2 settings, or none.</param>
    /// <param name="fields">The legacy fields, or none.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, PluginSetting[]? settings = null, PluginField[]? fields = null)
    {
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Test plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Plugin used by the required setting tests.",
            Type = PluginType.Payment,
            Category = "Payment",
            EntryPoint = typeof(ConfigurationCapturingPaymentPlugin).FullName!,
            SdkVersion = "1.0.0",
            Settings = settings ?? [],
            Fields = fields ?? [],
        };

        return new LoadedPlugin(manifest, typeof(ConfigurationCapturingPaymentPlugin));
    }

    /// <summary>Enables the plugin, optionally stores its required value, and activates it.</summary>
    /// <param name="plugin">The plugin to activate.</param>
    /// <param name="storedValue">The value stored under <see cref="RequiredKey"/>, or <see langword="null"/> for none.</param>
    /// <returns>The activation outcome.</returns>
    private async Task<PluginActivationOutcome> ActivateAsync(LoadedPlugin plugin, string? storedValue)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await PluginActivationTestSeeds.EnableAsync(db, plugin.Manifest.Id);
        if (storedValue is not null)
        {
            await PluginActivationTestSeeds.StoreAsync(db, plugin.Manifest.Id, RequiredKey, storedValue);
        }

        var activation = await PluginActivationTestSeeds.Activator(factory, db)
            .ActivateAsync<IPaymentPlugin>(plugin, CancellationToken.None);

        return activation.Outcome;
    }

    /// <summary>A required setting of a version 2 manifest that is stored lets the plugin activate.</summary>
    [Fact]
    public async Task RequiredSetting_Stored_Activates()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-set"),
            settings: [new PluginSetting { Key = RequiredKey, Label = "E", Type = PluginSettingType.Url, Required = true }]);

        (await ActivateAsync(plugin, "https://example.test")).Should().Be(PluginActivationOutcome.Activated);
    }

    /// <summary>A required setting of a version 2 manifest that is not stored keeps the plugin out.</summary>
    [Fact]
    public async Task RequiredSetting_Missing_IsRequiredSettingMissing()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-set"),
            settings: [new PluginSetting { Key = RequiredKey, Label = "E", Type = PluginSettingType.Url, Required = true }]);

        (await ActivateAsync(plugin, null)).Should().Be(PluginActivationOutcome.RequiredSettingMissing);
    }

    /// <summary>A required setting stored as blank text counts as missing.</summary>
    [Fact]
    public async Task RequiredSetting_Blank_IsRequiredSettingMissing()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-set"),
            settings: [new PluginSetting { Key = RequiredKey, Label = "E", Type = PluginSettingType.Text, Required = true }]);

        (await ActivateAsync(plugin, "   ")).Should().Be(PluginActivationOutcome.RequiredSettingMissing);
    }

    /// <summary>A manifest with only legacy fields still gates on its required field, through the fallback.</summary>
    [Fact]
    public async Task RequiredLegacyField_Missing_IsRequiredSettingMissing()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-fld"),
            fields: [new PluginField { Key = RequiredKey, Label = "E", Type = "text", Required = true }]);

        (await ActivateAsync(plugin, null)).Should().Be(PluginActivationOutcome.RequiredSettingMissing);
    }

    /// <summary>A manifest with only legacy fields activates once its required field is stored.</summary>
    [Fact]
    public async Task RequiredLegacyField_Stored_Activates()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-fld"),
            fields: [new PluginField { Key = RequiredKey, Label = "E", Type = "text", Required = true }]);

        (await ActivateAsync(plugin, "value")).Should().Be(PluginActivationOutcome.Activated);
    }

    /// <summary>When a manifest lists settings, a legacy required field no longer gates the plugin: the settings supersede.</summary>
    [Fact]
    public async Task SettingsSupersedeFields_LegacyRequiredFieldIsIgnored()
    {
        var plugin = Plugin(
            PluginActivationTestSeeds.NewId("req-mix"),
            settings: [new PluginSetting { Key = "optional", Label = "O", Type = PluginSettingType.Text }],
            fields: [new PluginField { Key = RequiredKey, Label = "E", Type = "text", Required = true }]);

        (await ActivateAsync(plugin, null)).Should().Be(PluginActivationOutcome.Activated);
    }
}
