using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A plugin upload folder with one app plugin installed and loaded into its own load context, and the reloader wired over it
/// as the host wires it, so a test can stage a version and reload, or unload, without a container or a database.
/// </summary>
internal sealed class PluginReloadHarness : IDisposable
{
    /// <summary>The installed plugin's id.</summary>
    public const string PluginId = "reload-plugin";

    /// <summary>The upload folder.</summary>
    public string Root { get; }

    /// <summary>The registry the plugin was loaded into.</summary>
    public PluginRegistry Registry { get; }

    /// <summary>The record of how each change ended.</summary>
    public PluginChangeStore Changes { get; }

    /// <summary>The reloader under test.</summary>
    public IPluginHotReloader Reloader { get; }

    /// <summary>Initializes the harness over a loaded plugin.</summary>
    /// <param name="root">The upload folder.</param>
    /// <param name="registry">The registry the plugin was loaded into.</param>
    /// <param name="changes">The record of how each change ended.</param>
    /// <param name="reloader">The reloader under test.</param>
    private PluginReloadHarness(string root, PluginRegistry registry, PluginChangeStore changes, IPluginHotReloader reloader)
    {
        Root = root;
        Registry = registry;
        Changes = changes;
        Reloader = reloader;
    }

    /// <summary>Installs version 1.0.0 of an app plugin, loads it and builds the reloader over it.</summary>
    /// <param name="drain">The longest the reloader waits for the running calls of the old version.</param>
    /// <returns>The harness; the caller disposes it.</returns>
    public static PluginReloadHarness Create(TimeSpan drain)
    {
        var root = PluginSigningTestSeeds.NewFolder("hot-reload");
        WriteVersion(Path.Combine(root, PluginId), "1.0.0", broken: false, type: "App");

        var services = new ServiceCollection();
        var verifier = PluginSigningTestSeeds.Verifier(enforced: false);
        PluginLoader.DiscoverAndRegister(
            services, [new PluginRoot(root, PluginOrigin.Uploaded)], verifier, allowUnsigned: false, PluginOptions.DefaultMaxViewBytes, NullLoggerFactory.Instance);
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<PluginRegistry>();
        var changes = provider.GetRequiredService<PluginChangeStore>();
        var reloader = new PluginHotReloader(
            registry,
            provider.GetRequiredService<IPluginIntegrationRegistry>(),
            provider.GetRequiredService<PluginFolderLoader>(),
            changes,
            new ClearingMigrationRetry(registry),
            new CountingAvailabilityInvalidator(),
            root,
            drain,
            () => false,
            NullLogger<PluginHotReloader>.Instance);
        return new PluginReloadHarness(root, registry, changes, reloader);
    }

    /// <summary>Stages a version of the plugin the way an upload does: in the staging folder of the upload folder.</summary>
    /// <param name="version">The staged version.</param>
    /// <param name="broken">Whether its assembly cannot be loaded.</param>
    /// <param name="type">The plugin type its manifest declares.</param>
    public void Stage(string version, bool broken = false, string type = "App")
    {
        WriteVersion(Path.Combine(Root, ".staged", PluginId), version, broken, type);
    }

    /// <summary>Reads the version of the manifest in the installed folder.</summary>
    /// <returns>The installed version text.</returns>
    public string InstalledVersion()
    {
        return Version(Path.Combine(Root, PluginId));
    }

    /// <summary>Reads the version of the manifest in a plugin folder.</summary>
    /// <param name="folder">The plugin folder.</param>
    /// <returns>The version text.</returns>
    public static string Version(string folder)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "plugin.json")));
        return document.RootElement.GetProperty("version").GetString()!;
    }

    /// <summary>Deletes the upload folder.</summary>
    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }

    /// <summary>Writes a plugin folder: its manifest and its assembly, which is unloadable when broken.</summary>
    /// <param name="folder">The folder to create.</param>
    /// <param name="version">The manifest version.</param>
    /// <param name="broken">Whether the assembly is not a loadable assembly.</param>
    /// <param name="type">The plugin type the manifest declares.</param>
    private static void WriteVersion(string folder, string version, bool broken, string type)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson(PluginId, type: type)
                .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal)
                .Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\"", StringComparison.Ordinal));
        File.WriteAllBytes(
            Path.Combine(folder, "Fixture.Reload.dll"),
            broken ? [0x4d, 0x5a, 0x00, 0x01] : PluginAssemblyFixtures.AppPlugin("Fixture.Reload", "Fixture.AppPlugin"));
    }

    /// <summary>The migration retry of the harness: no plugin has a hook, so it only lifts the gate the reload put up.</summary>
    private sealed class ClearingMigrationRetry : IPluginMigrationRetry
    {
        /// <summary>The registry whose gate is lifted.</summary>
        private readonly PluginRegistry _registry;

        /// <summary>Initializes the retry.</summary>
        /// <param name="registry">The registry whose gate is lifted.</param>
        public ClearingMigrationRetry(PluginRegistry registry)
        {
            _registry = registry;
        }

        /// <inheritdoc/>
        public Task RetryAsync(string pluginId, CancellationToken ct)
        {
            _registry.ClearMigrationPending(pluginId);
            return Task.CompletedTask;
        }
    }

    /// <summary>The availability invalidator of the harness, which has no cache to drop.</summary>
    private sealed class CountingAvailabilityInvalidator : IPluginAvailabilityInvalidator
    {
        /// <inheritdoc/>
        public void Invalidate()
        {
        }
    }
}
