using System.Reflection;
using Hostpanel.Sdk.Packaging.Naming;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Fulfilment;
using Hostpanel.Sdk.Plugins.Hosting;
using Hostpanel.Sdk.Plugins.Identity;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
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
    /// <summary>The longest load error kept for the admin list, in characters.</summary>
    private const int MaxLoadErrorLength = 300;

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
    /// part of the host. A refused plugin is skipped alone and logged with its id and the rule it breaks; a plugin
    /// refused for a broken manifest rule is also kept in <see cref="PluginRegistry"/> (<see cref="RefusedPlugin"/>), in
    /// either folder, so the admin plugin list can show it. A refused plugin claims its id like a loaded one: an upload
    /// of the same name never loads in its place and never replaces its refusal entry. A plugin with a pending upgrade
    /// record is flagged in the registry, which keeps it from being activated until its migration succeeds.
    /// </remarks>
    /// <param name="services">The application service collection.</param>
    /// <param name="pluginRoots">Absolute plugin folders with their origin, built-in first.</param>
    /// <param name="verifier">Checks the signature of an uploaded plugin and applies the host's policy.</param>
    /// <param name="allowUnsigned">Whether the admin setting "allow unsigned plugins" is on.</param>
    /// <param name="maxViewBytes">
    /// The largest view document a plugin may ship, in bytes (<c>Plugins:MaxViewBytes</c>); a larger one skips the plugin.
    /// </param>
    /// <param name="loggerFactory">Logger factory used during startup loading.</param>
    public static void DiscoverAndRegister(
        IServiceCollection services,
        IReadOnlyList<PluginRoot> pluginRoots,
        PluginSignatureVerifier verifier,
        bool allowUnsigned,
        long maxViewBytes,
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
            if (root.Origin == PluginOrigin.Uploaded)
            {
                // An upgrade whose migration has not succeeded keeps its plugin gated from the first moment, before the
                // runner has had a chance to run the hook.
                foreach (var marker in PluginUpgradeApplier.ReadPending(root.Path, logger))
                {
                    registry.MarkMigrationPending(marker.PluginId);
                }
            }

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
            var folderName = Path.GetFileName(pluginDir);
            if (!PluginIdentifier.IsWellFormed(folderName))
            {
                continue;
            }

            // A well-formed folder under a reserved id is a plugin installed before the id was reserved: it is never
            // loaded (the host's own segment would shadow its routes), but it is reported, so the operator knows why it
            // is missing and removes it from the plugins page. A plain folder whose manifest declares a reserved id is
            // refused by the manifest reader (PluginManifestRule.PluginIdInvalid), the one place the manifest id is checked.
            if (PluginIdentifier.IsReserved(folderName))
            {
                logger.LogWarning(
                    "Plugin folder {Dir} uses the reserved plugin id {Id}. Skipping it; remove it from the plugins page.",
                    pluginDir, folderName);
                continue;
            }

            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var manifest = reader.TryRead(manifestPath, out var violation);
            if (manifest is null)
            {
                // A refused plugin claims its id like a loaded one, so an upload with the same name can never stand in
                // for a built-in plugin that was refused, and can never overwrite that plugin's refusal entry.
                var folderId = Path.GetFileName(pluginDir);
                if (!claimedIds.Add(folderId))
                {
                    logger.LogWarning("Plugin folder {Dir} is refused and its id {Id} is already declared by an earlier folder, skipping", pluginDir, folderId);
                    continue;
                }

                // Only a broken manifest rule is kept: the admin list shows its rule and detail, and the plugin
                // is never activated. Any other unreadable manifest stays a log line.
                if (violation is not null)
                {
                    registry.RegisterRefused(new RefusedPlugin(folderId, violation.Rule, violation.Detail, origin == PluginOrigin.BuiltIn));
                }

                continue;
            }

            // Claimed by the first folder that declares it, loaded or not, so an upload can never
            // stand in for a built-in plugin that failed to load.
            if (!claimedIds.Add(manifest.Id))
            {
                logger.LogWarning("Plugin {Id} in {Dir} is already declared by an earlier folder, skipping", manifest.Id, pluginDir);
                continue;
            }

            var refusal = verifier.Check(pluginDir, manifest.Id, origin, allowUnsigned, logger).Refusal;
            if (refusal is not null)
            {
                logger.LogWarning(
                    "Plugin {Id} refused by signature rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                    manifest.Id, refusal.Rule, refusal.Detail);
                continue;
            }

            var views = PluginViewDocumentReader.Read(pluginDir, manifest, maxViewBytes);
            if (views.Violation is not null)
            {
                logger.LogWarning(
                    "Plugin {Id} refused by manifest rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                    manifest.Id, views.Violation.Rule, views.Violation.Detail);
                continue;
            }

            var icon = PluginIconReader.Read(pluginDir, manifest);
            if (icon.Violation is not null)
            {
                logger.LogWarning(
                    "Plugin {Id} refused by manifest rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                    manifest.Id, icon.Violation.Rule, icon.Violation.Detail);
                continue;
            }

            var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                logger.LogError("No DLL found for plugin {Id} in {Dir}, skipping", manifest.Id, pluginDir);
                RefuseLoad(registry, manifest.Id, origin, "the plugin folder holds no assembly");
                continue;
            }

            Type? pluginType = null;
            List<Exception> failures = [];
            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);

                    // throwOnError is on so a load that fails says why. With it off, GetType answers null for a type whose
                    // dependency cannot be loaded (the plugin was built against an SDK version this host does not have)
                    // exactly as for a type that is not there, and the log could only say "not found".
                    pluginType = assembly.GetType(manifest.EntryPoint, throwOnError: true);
                    break;
                }
                catch (TypeLoadException ex)
                {
                    // The usual case in a folder with helper assemblies: this one does not define the entry point. It is
                    // logged below, with the skip, only when no assembly of the plugin defines it.
                    failures.Add(ex);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load assembly {Dll} for plugin {Id}", dll, manifest.Id);
                    failures.Add(ex);
                }
            }

            if (pluginType is null)
            {
                // Every assembly was tried, so each one that did not yield the entry point left its error. The one that
                // was asked for the entry point and could not give it is the cause; a helper assembly that failed to
                // load is only the fallback.
                var cause = failures.OfType<TypeLoadException>().FirstOrDefault() ?? failures[0];
                logger.LogError(
                    cause,
                    "Entry point type '{EntryPoint}' not found for plugin {Id}, skipping",
                    manifest.EntryPoint, manifest.Id);

                // The plugin is installed and its manifest was accepted, so it must not look healthy in the admin list:
                // the load error is kept beside it.
                RefuseLoad(registry, manifest.Id, origin, FirstLine(cause.Message));
                continue;
            }

            // A plugin that declares the endpoints capability promises the host it can answer routes. Declaring it without
            // implementing the contract is a broken manifest, refused here like any other, and kept in the registry so the
            // admin list shows why. The id is already claimed above, so an upload cannot stand in for it.
            if (manifest.Declares(PluginCapabilities.Endpoints) && !PluginCapabilityRules.IsImplementedBy(PluginCapabilities.Endpoints, pluginType))
            {
                var detail = $"capabilities declares '{PluginCapabilities.Endpoints}' but '{manifest.EntryPoint}' does not implement IPluginEndpoints";
                logger.LogWarning(
                    "Plugin {Id} refused by manifest rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                    manifest.Id, PluginManifestRule.EndpointsNotImplemented, detail);
                registry.RegisterRefused(new RefusedPlugin(
                    manifest.Id, PluginManifestRule.EndpointsNotImplemented, detail, origin == PluginOrigin.BuiltIn));
                continue;
            }

            // A rule added after the plugin was installed does not take it away: it was accepted once and may be
            // running payments. Logged only here, after the icon check and the assembly load, so a plugin skipped by a
            // later check never gets the line. The author sees the fault here and an upload of a new version is refused.
            foreach (var advisory in views.Advisories)
            {
                logger.LogWarning(
                    "Plugin {Id} breaks view rule {Rule}: {Detail}. It stays loaded; a new upload would be refused.",
                    manifest.Id, advisory.Rule, advisory.Detail);
            }

            // Read last, like the advisories above, so a plugin skipped by an earlier check never gets a warning about its agent files.
            var agentProfile = ReadAgentProfile(registry, manifest, pluginDir, logger);

            RegisterPluginType(services, manifest, pluginType, logger);
            registry.Register(new LoadedPlugin(manifest, pluginType, views.Views, icon.Icon, agentProfile));

            if (manifest.ShowInIntegrations)
            {
                integrationRegistry.Register(new PluginIntegrationEntry(
                    Slug: manifest.Id,
                    Name: manifest.Name,
                    Category: manifest.IntegrationsCategory ?? manifest.Category,
                    Type: manifest.Type,
                    Color: manifest.Color,
                    RequiredSettingKeys: manifest.GetSettings()
                        .Where(s => s.Required)
                        .Select(s => s.Key)
                        .ToList(),
                    IconUrl: icon.Icon is null ? null : PluginIconPaths.For(manifest.Id)));

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
    /// Reads the SSH key agent profile a plugin's manifest names. A rule added after the plugin was installed does not take
    /// it away (it was accepted once and may be running payments): a refused profile, or an id another loaded plugin already
    /// ships, is only warned about and the plugin loads without a profile; an upload of a new version is refused at install.
    /// </summary>
    /// <param name="registry">The registry the loader fills, asked whether the profile id is already taken.</param>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="pluginDir">The plugin folder.</param>
    /// <param name="logger">Records the warning.</param>
    /// <returns>The checked profile, or <see langword="null"/> when the plugin ships none or its profile is not usable.</returns>
    internal static PluginAgentProfileDto? ReadAgentProfile(PluginRegistry registry, PluginManifest manifest, string pluginDir, ILogger logger)
    {
        var agent = PluginAgentReader.Read(pluginDir, manifest);
        if (agent.Violation is not null)
        {
            logger.LogWarning(
                "Plugin {Id} breaks agent rule {Rule}: {Detail}. It stays loaded without its agent profile; a new upload would be refused.",
                manifest.Id, agent.Violation.Rule, agent.Violation.Detail);
            return null;
        }

        if (agent.Profile is not null && registry.FindAgentProfile(agent.Profile.ProfileId) is { } taken)
        {
            logger.LogWarning(
                "Plugin {Id} names the agent profile {Profile}, which plugin {OtherId} already ships. It stays loaded without its agent profile.",
                manifest.Id, agent.Profile.ProfileId, taken.PluginId);
            return null;
        }

        return agent.Profile;
    }

    /// <summary>
    /// Keeps an installed plugin that could not be loaded in the registry, so the admin list shows it with the reason.
    /// </summary>
    /// <param name="registry">The registry the loader fills.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="origin">Where the plugin folder is.</param>
    /// <param name="detail">Why the plugin did not load.</param>
    private static void RefuseLoad(PluginRegistry registry, string pluginId, PluginOrigin origin, string detail)
    {
        registry.RegisterRefused(new RefusedPlugin(pluginId, PluginManifestRule.AssemblyLoadFailed, detail, origin == PluginOrigin.BuiltIn));
    }

    /// <summary>
    /// Cuts a load error to its first line and a fixed length: the admin list shows what failed, never a stack.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <returns>The first line of the message, at most <see cref="MaxLoadErrorLength"/> characters.</returns>
    private static string FirstLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length <= MaxLoadErrorLength ? line : line[..MaxLoadErrorLength];
    }

    /// <summary>
    /// Registers a plugin type as a keyed scoped service under the SDK contract of its <see cref="PluginType"/>, and
    /// also under <see cref="IPluginEndpoints"/> when a plugin of another type declares the <c>endpoints</c> capability.
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
        // Every plugin that reaches the network asks IHttpClientFactory for a client named after its
        // own id. An unregistered name would silently get the factory's 100-second client with no
        // handler, so each loaded plugin gets the generic plugin profile under its id: the host
        // names no vendor, and the contract with the plugin is only "your id is your client name".
        services.AddHttpClient(manifest.Id, client => client.Timeout = HttpBackstopTimeouts.Plugin)
            .AddNoRedirects()
            .AddPluginResilience();

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
                // An app plugin is registered as its endpoints below, like any plugin that declares the capability.
                break;
            default:
                logger.LogWarning("Unknown plugin type {Type} for plugin {Id}, skipping DI registration",
                    manifest.Type, manifest.Id);
                break;
        }

        // The same class answers routes too; an app plugin always serves them, any other type only with the capability.
        if (manifest.ServesEndpoints())
        {
            services.AddKeyedScoped(typeof(IPluginEndpoints), manifest.Id, pluginType);
        }
    }
}
