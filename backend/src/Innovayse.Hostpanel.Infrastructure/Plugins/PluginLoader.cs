using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Scans the plugin folders at startup (the build's own <c>plugins/</c> folder, then the upload
/// folder), loads each plugin through <see cref="PluginFolderLoader"/> and records it in the
/// registries.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Discovers all valid plugins under each of <paramref name="pluginRoots"/>, in order, loads each into a collectible
    /// load context of its own, and records them in a singleton <see cref="PluginRegistry"/>. A plugin id already loaded
    /// from an earlier root is skipped, so a built-in plugin always wins over an upload with the same id; a folder whose
    /// name breaks the plugin id rule (such as the upload folder's staging area) is never a plugin.
    /// Also registers a singleton <see cref="PluginIntegrationRegistry"/> for plugins that expose themselves as admin
    /// integrations, the named HTTP client of every loaded plugin, and the <see cref="PluginFolderLoader"/> a reload uses.
    /// </summary>
    /// <remarks>
    /// The service collection keeps no plugin type: a plugin is built per call by <see cref="PluginActivator"/> from the
    /// registry, so the type is held only by the registry and a plugin version can be unloaded when the registry lets go of
    /// it. Each folder is checked and loaded by <see cref="PluginFolderLoader"/>.
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
        var registry = new PluginRegistry();
        var integrationRegistry = new PluginIntegrationRegistry();
        var installRoot = pluginRoots.Where(r => r.Origin == PluginOrigin.Uploaded).Select(r => r.Path).FirstOrDefault() ?? string.Empty;
        var changes = new PluginChangeStore(installRoot, loggerFactory.CreateLogger<PluginChangeStore>());
        var folderLoader = new PluginFolderLoader(registry, integrationRegistry, verifier, changes, installRoot, maxViewBytes, loggerFactory);

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
            var plugin = folderLoader.LoadChanged(pluginDir, origin, claimedIds, allowUnsigned, reported: false);
            if (plugin is not null)
            {
                RegisterHttpClient(services, plugin.Manifest.Id);
            }
        }

        // One instance under both names: the resolver reads the concrete registry, handlers the port.
        services.AddSingleton(registry);
        services.AddSingleton<IPluginRegistry>(registry);
        services.AddSingleton<IPluginIntegrationRegistry>(integrationRegistry);
        services.AddSingleton(folderLoader);
        services.AddSingleton(changes);
        services.AddSingleton<IPluginChanges>(changes);
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
    /// Registers the named HTTP client a plugin reaches the network through. Every plugin that does asks
    /// <see cref="IHttpClientFactory"/> for a client named after its own id; an unregistered name would silently get the
    /// factory's 100-second client with no handler, so each loaded plugin gets the generic plugin profile under its id: the
    /// host names no vendor, and the contract with the plugin is only "your id is your client name".
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="pluginId">The plugin id, which names the client.</param>
    internal static void RegisterHttpClient(IServiceCollection services, string pluginId)
    {
        services.AddHttpClient(pluginId, client => client.Timeout = HttpBackstopTimeouts.Plugin)
            .AddNoRedirects()
            .AddPluginResilience();
    }
}
