using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// What the fulfilment, identity and hosted plugin tests share: a manifest for a test plugin, its settings rows
/// and a <see cref="PluginActivator"/> over the real database, so each test only states what differs.
/// </summary>
internal static class PluginActivationTestSeeds
{
    /// <summary>The key of the one setting a manifest built with <c>requiresApiKey</c> marks required.</summary>
    public const string ApiKeyField = "api_key";

    /// <summary>A plugin id no other test uses, so the settings rows of two tests never share a key.</summary>
    /// <param name="prefix">A readable start for the id; ids sort by it, which is the order the resolvers return.</param>
    /// <returns>The id.</returns>
    public static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Builds a loaded plugin whose manifest declares <paramref name="type"/>.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The plugin type the manifest declares.</param>
    /// <param name="implementation">The CLR type registered as the entry point.</param>
    /// <param name="requiresApiKey">Whether the manifest marks <see cref="ApiKeyField"/> required.</param>
    /// <returns>The loaded plugin, as the loader registers it.</returns>
    public static LoadedPlugin Plugin(string id, PluginType type, Type implementation, bool requiresApiKey = false)
    {
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Test plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Plugin used by the plugin runtime tests.",
            Type = type,
            Category = type.ToString(),
            EntryPoint = implementation.FullName!,
            SdkVersion = "1.0.0",
            Fields = requiresApiKey
                ? [new PluginField { Key = ApiKeyField, Label = "API key", Type = "secret", Required = true }]
                : [],
        };

        return new LoadedPlugin(manifest, implementation);
    }

    /// <summary>Builds a registry holding <paramref name="plugins"/>.</summary>
    /// <param name="plugins">The plugins the registry reports as loaded.</param>
    /// <returns>The registry.</returns>
    public static PluginRegistry Registry(params LoadedPlugin[] plugins)
    {
        var registry = new PluginRegistry();
        foreach (var plugin in plugins)
        {
            registry.Register(plugin);
        }

        return registry;
    }

    /// <summary>Stores the <c>is_enabled</c> setting of each plugin in <paramref name="pluginIds"/> as <c>true</c>.</summary>
    /// <param name="db">The data context to write through.</param>
    /// <param name="pluginIds">The plugins to switch on.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    public static async Task EnableAsync(AppDbContext db, params string[] pluginIds)
    {
        foreach (var id in pluginIds)
        {
            db.Settings.Add(Setting.Create(IntegrationSettingKeys.EnabledKey(id), "true", null));
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Stores one plugin setting row.</summary>
    /// <param name="db">The data context to write through.</param>
    /// <param name="pluginId">The plugin the setting belongs to.</param>
    /// <param name="field">The field key inside the plugin's section.</param>
    /// <param name="value">The stored value.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    public static async Task StoreAsync(AppDbContext db, string pluginId, string field, string value)
    {
        db.Settings.Add(Setting.Create(IntegrationSettingKeys.FieldKey(pluginId, field), value, null));
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Builds the activator over <paramref name="db"/>, with an empty host configuration.</summary>
    /// <param name="factory">The shared host, for the settings cipher.</param>
    /// <param name="db">The data context the activator reads settings from; the caller keeps it alive.</param>
    /// <param name="services">The container plugins are built from; an empty one when the plugins need nothing else.</param>
    /// <returns>The activator.</returns>
    public static PluginActivator Activator(IntegrationTestFactory factory, AppDbContext db, IServiceProvider? services = null)
    {
        return new PluginActivator(
            db,
            factory.Services.GetRequiredService<ISecretSettingCipher>(),
            services ?? new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<PluginActivator>.Instance);
    }
}
