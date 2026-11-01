using Innovayse.Hostpanel.Application.Admin.Integrations.Commands.SaveIntegrationConfig;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// The integration detail read and the save for a plugin whose manifest is version 2: a secret setting is
/// answered as set or not set and never by value, and is stored encrypted. Both handlers run over the real
/// database with a registry that holds the test plugin.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginIntegrationSecretTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The clear text the tests save as a secret; it must never come back or be stored as written.</summary>
    private const string ClearSecret = "clear-secret-should-never-be-echoed";

    /// <summary>Builds a registry holding one plugin with the given settings.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="settings">The version 2 settings the manifest declares.</param>
    /// <returns>The registry.</returns>
    private static PluginRegistry Registry(string id, params PluginSetting[] settings)
    {
        var registry = new PluginRegistry();
        registry.Register(new LoadedPlugin(
            new PluginManifest
            {
                Id = id,
                Name = "Settings plugin",
                Version = "1.0.0",
                Author = "Tests",
                Description = "Plugin used by the integration secret tests.",
                Type = PluginType.Payment,
                Category = "Payment",
                EntryPoint = typeof(object).FullName!,
                SdkVersion = "1.0",
                Settings = settings,
            },
            typeof(object)));
        return registry;
    }

    /// <summary>The two settings every test uses: a secret credential and a plain address.</summary>
    /// <returns>The settings.</returns>
    private static PluginSetting[] Settings()
    {
        return
        [
            new PluginSetting { Key = "apiKey", Label = "API key", Type = PluginSettingType.Secret, Required = true },
            new PluginSetting { Key = "host", Label = "Host", Type = PluginSettingType.Url },
        ];
    }

    /// <summary>Saves a secret and a plain value for the plugin through the real save handler.</summary>
    /// <param name="scope">The scope the handler's services come from.</param>
    /// <param name="registry">The registry holding the plugin.</param>
    /// <param name="id">The plugin id.</param>
    /// <returns>A task that completes when the save has finished.</returns>
    private static async Task SaveAsync(IServiceScope scope, PluginRegistry registry, string id)
    {
        var handler = new SaveIntegrationConfigHandler(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<IMemoryCache>(),
            scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>(),
            registry,
            scope.ServiceProvider.GetRequiredService<ICurrentUserService>(),
            NullLogger<SaveIntegrationConfigHandler>.Instance);

        var result = await handler.HandleAsync(
            new SaveIntegrationConfigCommand(
                id,
                true,
                new Dictionary<string, string?> { ["apiKey"] = ClearSecret, ["host"] = "https://example.test" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>Saving a secret setting stores it encrypted and the plain setting as written.</summary>
    [Fact]
    public async Task SaveIntegrationConfig_SecretSetting_IsStoredEncrypted()
    {
        var id = PluginActivationTestSeeds.NewId("secret-save");
        using var scope = factory.Services.CreateScope();

        await SaveAsync(scope, Registry(id, Settings()), id);

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>();
        var secret = await db.Settings.AsNoTracking().SingleAsync(s => s.Key == IntegrationSettingKeys.FieldKey(id, "apiKey"));
        var plain = await db.Settings.AsNoTracking().SingleAsync(s => s.Key == IntegrationSettingKeys.FieldKey(id, "host"));
        secret.Value.Should().NotBe(ClearSecret).And.NotContain(ClearSecret);
        cipher.IsProtected(secret.Value).Should().BeTrue();
        cipher.Reveal(secret.Value).Should().Be(ClearSecret);
        plain.Value.Should().Be("https://example.test");
    }

    /// <summary>Reading the integration after a save answers the secret as set, with no value, and the plain value in clear.</summary>
    [Fact]
    public async Task GetIntegration_SecretSetting_ReturnsNoValueAndIsSecret()
    {
        var id = PluginActivationTestSeeds.NewId("secret-get");
        var registry = Registry(id, Settings());
        using var scope = factory.Services.CreateScope();
        await SaveAsync(scope, registry, id);

        var handler = new GetIntegrationHandler(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(), registry);
        var result = await handler.HandleAsync(new GetIntegrationQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var detail = result.Value!;
        var secret = detail.Config["apiKey"];
        secret.IsSecret.Should().BeTrue();
        secret.HasValue.Should().BeTrue();
        secret.Value.Should().BeNull();
        var plain = detail.Config["host"];
        plain.IsSecret.Should().BeFalse();
        plain.Value.Should().Be("https://example.test");
        detail.FieldDefinitions.Single(f => f.Key == "apiKey").Type.Should().Be(IntegrationFieldType.Secret);
        detail.FieldDefinitions.Single(f => f.Key == "apiKey").Required.Should().BeTrue();
        detail.FieldDefinitions.Single(f => f.Key == "host").Type.Should().Be(IntegrationFieldType.Text);
    }
}
