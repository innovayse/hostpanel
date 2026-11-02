using System.Reflection;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Hosting;
using Innovayse.SDK.Plugins.Identity;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Payment;
using Innovayse.SDK.Plugins.Provisioning;
using Innovayse.SDK.Plugins.Registrar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Scans the plugin folders at startup (the build's own <c>plugins/</c> folder, then the upload
/// folder), validates manifests, loads assemblies, and registers provider implementations in the
/// DI container.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Discovers all valid plugins under each of <paramref name="pluginRoots"/>, in order, loads their
    /// assemblies, and registers them as keyed scoped services in <paramref name="services"/>. A
    /// plugin id already loaded from an earlier root is skipped, so a built-in plugin always wins
    /// over an upload with the same id; a folder whose name breaks the plugin id rule (such as the
    /// upload folder's staging area) is never a plugin.
    /// Also registers a singleton <see cref="PluginRegistry"/> populated with every loaded plugin,
    /// and a singleton <see cref="PluginIntegrationRegistry"/> for plugins that expose themselves
    /// as admin integrations.
    /// </summary>
    /// <remarks>
    /// An uploaded plugin is signature-checked before any of its assemblies is loaded, because loading an assembly
    /// can already run its code. A plugin shipped with the host (<see cref="PluginOrigin.BuiltIn"/>) is not: it is
    /// part of the host. A refused plugin is skipped alone and logged with its id and the rule it breaks.
    /// </remarks>
    /// <param name="services">The application service collection.</param>
    /// <param name="pluginRoots">Absolute plugin folders with their origin, built-in first.</param>
    /// <param name="verifier">Checks the signature of an uploaded plugin and applies the host's policy.</param>
    /// <param name="allowUnsigned">Whether the admin setting "allow unsigned plugins" is on.</param>
    /// <param name="loggerFactory">Logger factory used during startup loading.</param>
    public static void DiscoverAndRegister(
        IServiceCollection services,
        IReadOnlyList<PluginRoot> pluginRoots,
        PluginSignatureVerifier verifier,
        bool allowUnsigned,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(PluginLoader));
        var manifestLogger = loggerFactory.CreateLogger<PluginManifestReader>();
        var reader = new PluginManifestReader(manifestLogger);
        var registry = new PluginRegistry();
        var integrationRegistry = new PluginIntegrationRegistry();

        var claimedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pluginDirs = new List<(string Dir, PluginOrigin Origin)>();
        foreach (var root in pluginRoots)
        {
            if (Directory.Exists(root.Path))
            {
                pluginDirs.AddRange(Directory.GetDirectories(root.Path).Select(dir => (dir, root.Origin)));
            }
            else
            {
                logger.LogInformation("Plugins directory not found at {Path}", root.Path);
            }
        }

        foreach (var (pluginDir, origin) in pluginDirs)
        {
            if (!PluginIdentifier.IsValid(Path.GetFileName(pluginDir)))
            {
                continue;
            }

            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var manifest = reader.TryRead(manifestPath);
            if (manifest is null)
            {
                continue;
            }

            // Claimed by the first folder that declares it, loaded or not, so an upload can never
            // stand in for a built-in plugin that failed to load.
            if (!claimedIds.Add(manifest.Id))
            {
                logger.LogWarning("Plugin {Id} in {Dir} is already declared by an earlier folder — skipping", manifest.Id, pluginDir);
                continue;
            }

            var refusal = verifier.Check(pluginDir, manifest.Id, origin, allowUnsigned, logger);
            if (refusal is not null)
            {
                logger.LogWarning(
                    "Plugin {Id} refused by signature rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                    manifest.Id, refusal.Rule, refusal.Detail);
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
                    Type: manifest.Type,
                    Color: manifest.Color,
                    FieldDefinitions: manifest.GetSettings()
                        .Select(s => s.ToFieldDefinition())
                        .ToList()));

                logger.LogInformation(
                    "Plugin {Id} registered as integration entry in category '{Category}'",
                    manifest.Id,
                    manifest.IntegrationsCategory ?? manifest.Category);
            }

            logger.LogInformation("Plugin {Id} v{Version} loaded", manifest.Id, manifest.Version);
        }

        // One instance under both names: the resolver reads the concrete registry, handlers the port.
        services.AddSingleton(registry);
        services.AddSingleton<IPluginRegistry>(registry);
        services.AddSingleton<IPluginIntegrationRegistry>(integrationRegistry);
    }

    /// <summary>
    /// Registers a plugin type as a keyed scoped service under the SDK contract of its <see cref="PluginType"/>.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="pluginType">The concrete type to register.</param>
    /// <param name="logger">Logger for warnings.</param>
    internal static void RegisterPluginType(
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
            case PluginType.Identity:
                services.AddKeyedScoped(typeof(IClientResolver), manifest.Id, pluginType);
                break;
            case PluginType.Fulfilment:
                services.AddKeyedScoped(typeof(IFulfilmentPlugin), manifest.Id, pluginType);
                break;
            case PluginType.Hosted:
                services.AddKeyedScoped(typeof(IHostedPlugin), manifest.Id, pluginType);
                break;
            case PluginType.App:
                services.AddKeyedScoped(typeof(IPluginEndpoints), manifest.Id, pluginType);
                break;
            default:
                logger.LogWarning("Unknown plugin type {Type} for plugin {Id} — skipping DI registration",
                    manifest.Type, manifest.Id);
                break;
        }
    }
}
