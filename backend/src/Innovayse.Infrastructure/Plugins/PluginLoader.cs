namespace Innovayse.Infrastructure.Plugins;

using System.Reflection;
using Innovayse.Application.Admin.Integrations;
using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scans the <c>/plugins/</c> directory at startup, validates manifests, loads assemblies,
/// and registers provider implementations in the DI container.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Discovers all valid plugins under <paramref name="pluginsRoot"/>, loads their assemblies,
    /// and registers them as keyed scoped services in <paramref name="services"/>.
    /// Also registers a singleton <see cref="PluginRegistry"/> populated with every loaded plugin,
    /// and a singleton <see cref="PluginIntegrationRegistry"/> for plugins that expose themselves
    /// as admin integrations.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="pluginsRoot">Absolute path to the plugins directory.</param>
    /// <param name="loggerFactory">Logger factory used during startup loading.</param>
    public static void DiscoverAndRegister(
        IServiceCollection services,
        string pluginsRoot,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(PluginLoader));
        var manifestLogger = loggerFactory.CreateLogger<PluginManifestReader>();
        var reader = new PluginManifestReader(manifestLogger);
        var registry = new PluginRegistry();
        var integrationRegistry = new PluginIntegrationRegistry();

        if (!Directory.Exists(pluginsRoot))
        {
            logger.LogInformation("Plugins directory not found at {Path} — no plugins loaded", pluginsRoot);
            services.AddSingleton(registry);
            services.AddSingleton<IPluginIntegrationRegistry>(integrationRegistry);
            return;
        }

        foreach (var pluginDir in Directory.GetDirectories(pluginsRoot))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var manifest = reader.TryRead(manifestPath);
            if (manifest is null)
            {
                continue;
            }

            var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                logger.LogError("No DLL found for plugin {Id} in {Dir} — skipping", manifest.Id, pluginDir);
                continue;
            }

            Type? pluginType = null;
            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    pluginType = assembly.GetType(manifest.EntryPoint);
                    if (pluginType is not null)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load assembly {Dll} for plugin {Id}", dll, manifest.Id);
                }
            }

            if (pluginType is null)
            {
                logger.LogError(
                    "Entry point type '{EntryPoint}' not found for plugin {Id} — skipping",
                    manifest.EntryPoint, manifest.Id);
                continue;
            }

            RegisterPluginType(services, manifest, pluginType, logger);
            registry.Register(new LoadedPlugin(manifest, pluginType));

            if (manifest.ShowInIntegrations)
            {
                integrationRegistry.Register(new PluginIntegrationEntry(
                    Slug: manifest.Id,
                    Name: manifest.Name,
                    Category: manifest.IntegrationsCategory ?? manifest.Category,
                    Color: manifest.Color,
                    FieldDefinitions: manifest.Fields
                        .Select(f => new FieldDefinitionDto(f.Key, f.Label, f.Type, f.Required))
                        .ToList()));

                logger.LogInformation(
                    "Plugin {Id} registered as integration entry in category '{Category}'",
                    manifest.Id,
                    manifest.IntegrationsCategory ?? manifest.Category);
            }

            logger.LogInformation("Plugin {Id} v{Version} loaded", manifest.Id, manifest.Version);
        }

        services.AddSingleton(registry);
        services.AddSingleton<IPluginIntegrationRegistry>(integrationRegistry);
    }

    /// <summary>
    /// Registers a plugin type as a keyed scoped service based on its <see cref="PluginType"/>.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="pluginType">The concrete type to register.</param>
    /// <param name="logger">Logger for warnings.</param>
    private static void RegisterPluginType(
        IServiceCollection services,
        PluginManifest manifest,
        Type pluginType,
        ILogger logger)
    {
        switch (manifest.Type)
        {
            case PluginType.Provisioning:
                services.AddKeyedScoped(typeof(IProvisioningPlugin), manifest.Id, pluginType);
                break;
            case PluginType.Payment:
                services.AddKeyedScoped(typeof(IPaymentPlugin), manifest.Id, pluginType);
                break;
            case PluginType.Registrar:
                services.AddKeyedScoped(typeof(IRegistrarPlugin), manifest.Id, pluginType);
                break;
            default:
                logger.LogWarning("Unknown plugin type {Type} for plugin {Id} — skipping DI registration",
                    manifest.Type, manifest.Id);
                break;
        }
    }
}
