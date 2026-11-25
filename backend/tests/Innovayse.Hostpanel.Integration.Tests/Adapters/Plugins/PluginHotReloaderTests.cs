using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Hostpanel.Sdk.Plugins.Endpoints;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginHotReloader"/> applies a plugin change inside the running process: a staged version is swapped in once the
/// calls of the old one have finished, the old load context is unloaded, a version that does not load is rolled back to the
/// previous one, and anything that cannot be applied in place is left for the restart. No container or database: the plugin is a
/// real assembly loaded into a real collectible load context.
/// </summary>
public sealed class PluginHotReloaderTests
{
    /// <summary>A drain long enough for a test that waits for a call to finish, short enough to fail fast.</summary>
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(10);

    /// <summary>A plugin is loaded into a collectible context of its own and shares the host's SDK types.</summary>
    [Fact]
    public void Load_PutsThePluginInItsOwnCollectibleContextThatSharesTheHostSdk()
    {
        using var harness = PluginReloadHarness.Create(Patient);

        var plugin = harness.Registry.Find(PluginReloadHarness.PluginId)!;

        plugin.LoadContext.Should().NotBeNull();
        plugin.LoadContext!.IsCollectible.Should().BeTrue();
        AssemblyLoadContext.GetLoadContext(plugin.ImplementationType.Assembly).Should().BeSameAs(plugin.LoadContext);
        typeof(IPluginEndpoints).IsAssignableFrom(plugin.ImplementationType).Should().BeTrue("the SDK is the host's own, so the cast works");
    }

    /// <summary>A staged version is swapped in, the change is recorded as applied and the rollback copy is dropped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_SwapsTheStagedVersionInWithoutARestartAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);
        harness.Stage("1.1.0");

        var outcome = await harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.Applied);
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Manifest.Version.Should().Be("1.1.0");
        harness.InstalledVersion().Should().Be("1.1.0");
        Directory.Exists(Path.Combine(harness.Root, ".staged", PluginReloadHarness.PluginId)).Should().BeFalse();
        Directory.Exists(Path.Combine(harness.Root, ".previous", PluginReloadHarness.PluginId)).Should().BeFalse();
        var change = harness.Changes.Find(PluginReloadHarness.PluginId)!;
        change.Result.Should().Be(PluginChangeResult.Applied);
        change.FromVersion.Should().Be("1.0.0");
        change.ToVersion.Should().Be("1.1.0");
        harness.Registry.IsMigrationPending(PluginReloadHarness.PluginId).Should().BeFalse();
    }

    /// <summary>The reload waits for a call that is still running in the old version and only then swaps it out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_WaitsForARunningCallToFinishBeforeItSwapsAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);
        harness.Stage("1.1.0");
        var running = harness.Registry.Find(PluginReloadHarness.PluginId)!;
        running.Usage.Enter();

        var reload = harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);
        await Task.Delay(300);

        reload.IsCompleted.Should().BeFalse("a call of the old version is still running");
        harness.Registry.IsMigrationPending(PluginReloadHarness.PluginId).Should().BeTrue("no new call may start while the plugin drains");
        harness.InstalledVersion().Should().Be("1.0.0", "the files are not touched while a call still uses them");

        running.Usage.Leave();

        (await reload).Should().Be(PluginReloadOutcome.Applied);
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Manifest.Version.Should().Be("1.1.0");
    }

    /// <summary>A call that does not finish within the drain time leaves the plugin exactly as it was and the change for the restart.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_ACallThatOutlastsTheDrain_FallsBackToTheRestartAsync()
    {
        using var harness = PluginReloadHarness.Create(TimeSpan.FromMilliseconds(100));
        harness.Stage("1.1.0");
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Usage.Enter();

        var outcome = await harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.NeedsRestart);
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Manifest.Version.Should().Be("1.0.0");
        harness.InstalledVersion().Should().Be("1.0.0");
        Directory.Exists(Path.Combine(harness.Root, ".staged", PluginReloadHarness.PluginId)).Should().BeTrue("the staged version waits for the restart");
        harness.Registry.IsMigrationPending(PluginReloadHarness.PluginId).Should().BeFalse("the gate is lifted, the old version goes on serving");
    }

    /// <summary>A new version that cannot be loaded is rolled back: the previous version runs again and the failure is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_ANewVersionThatDoesNotLoad_RestoresThePreviousVersionAndRecordsTheFailureAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);
        harness.Stage("2.0.0", broken: true);

        var outcome = await harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.Failed);
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Manifest.Version.Should().Be("1.0.0", "the previous version is loaded again");
        harness.InstalledVersion().Should().Be("1.0.0");
        harness.Registry.IsMigrationPending(PluginReloadHarness.PluginId).Should().BeFalse();
        var change = harness.Changes.Find(PluginReloadHarness.PluginId)!;
        change.Result.Should().Be(PluginChangeResult.Failed);
        change.FromVersion.Should().Be("1.0.0");
        change.ToVersion.Should().Be("2.0.0");
        change.RestoredVersion.Should().Be("1.0.0");
        change.Error.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>A plugin that is not loaded, one whose type changes and one that becomes hosted cannot be reloaded in place.</summary>
    /// <param name="type">The plugin type the staged manifest declares.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Payment")]
    [InlineData("Hosted")]
    public async Task Reload_AChangeThatCannotBeAppliedInPlace_IsLeftForTheRestartAsync(string type)
    {
        using var harness = PluginReloadHarness.Create(Patient);
        harness.Stage("1.1.0", type: type);

        var outcome = await harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.NeedsRestart);
        harness.InstalledVersion().Should().Be("1.0.0");
        Directory.Exists(Path.Combine(harness.Root, ".staged", PluginReloadHarness.PluginId)).Should().BeTrue();
    }

    /// <summary>A plugin id that was never loaded needs the restart, because the host registers per plugin id at startup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_APluginThatIsNotLoaded_IsLeftForTheRestartAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);

        var outcome = await harness.Reloader.ReloadAsync("never-loaded", CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.NeedsRestart);
    }

    /// <summary>An unload takes the plugin out of the registry once its calls have finished.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unload_TakesThePluginOutOfTheRegistryAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);

        var outcome = await harness.Reloader.UnloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.Applied);
        harness.Registry.IsLoaded(PluginReloadHarness.PluginId).Should().BeFalse();
    }

    /// <summary>A removal waits for the running calls too, and gives up after the drain time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unload_ACallThatOutlastsTheDrain_LeavesThePluginLoadedAsync()
    {
        using var harness = PluginReloadHarness.Create(TimeSpan.FromMilliseconds(100));
        harness.Registry.Find(PluginReloadHarness.PluginId)!.Usage.Enter();

        var outcome = await harness.Reloader.UnloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.NeedsRestart);
        harness.Registry.IsLoaded(PluginReloadHarness.PluginId).Should().BeTrue();
    }

    /// <summary>
    /// The old version's load context is really freed after a reload: the host keeps no strong reference to a plugin type outside
    /// the context, so the runtime can collect it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reload_TheOldLoadContextIsCollectedAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);
        harness.Stage("1.1.0");
        var oldContext = WeakContextOf(harness);

        var outcome = await harness.Reloader.ReloadAsync(PluginReloadHarness.PluginId, CancellationToken.None);

        outcome.Should().Be(PluginReloadOutcome.Applied);
        IsCollected(oldContext).Should().BeTrue("nothing may hold the old plugin's types once the reload is over");
    }

    /// <summary>A removed plugin's load context is collected as well.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unload_TheLoadContextIsCollectedAsync()
    {
        using var harness = PluginReloadHarness.Create(Patient);
        var context = WeakContextOf(harness);

        (await harness.Reloader.UnloadAsync(PluginReloadHarness.PluginId, CancellationToken.None)).Should().Be(PluginReloadOutcome.Applied);

        IsCollected(context).Should().BeTrue();
    }

    /// <summary>
    /// At startup a new version that does not load is rolled back before the host serves anything: the previous version is
    /// loaded, the failure is recorded for the activity log and nothing is retried, so the host cannot loop.
    /// </summary>
    [Fact]
    public void Startup_ANewVersionThatDoesNotLoad_IsRolledBackAndRecorded()
    {
        var root = PluginSigningTestSeeds.NewFolder("startup-rollback");
        try
        {
            var previous = Path.Combine(root, ".previous", PluginReloadHarness.PluginId);
            WriteBuilt(previous, "1.0.0", broken: false);
            WriteBuilt(Path.Combine(root, PluginReloadHarness.PluginId), "2.0.0", broken: true);
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<PluginRegistry>();
            var changes = provider.GetRequiredService<PluginChangeStore>();
            registry.Find(PluginReloadHarness.PluginId)!.Manifest.Version.Should().Be("1.0.0");
            registry.GetRefusedPlugins().Should().BeEmpty("the refusal belonged to the version that was taken out");
            var change = changes.Find(PluginReloadHarness.PluginId)!;
            change.Result.Should().Be(PluginChangeResult.Failed);
            change.RestoredVersion.Should().Be("1.0.0");
            changes.Unreported().Should().ContainSingle(c => c.PluginId == PluginReloadHarness.PluginId, "the restart that applied it reports it to the activity log");
            Directory.Exists(Path.Combine(root, ".previous", PluginReloadHarness.PluginId)).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>At startup a new version that loads is final: the rollback copy goes and the change is recorded as applied.</summary>
    [Fact]
    public void Startup_ANewVersionThatLoads_DropsTheRollbackCopyAndRecordsTheChange()
    {
        var root = PluginSigningTestSeeds.NewFolder("startup-applied");
        try
        {
            WriteBuilt(Path.Combine(root, ".previous", PluginReloadHarness.PluginId), "1.0.0", broken: false);
            WriteBuilt(Path.Combine(root, PluginReloadHarness.PluginId), "1.1.0", broken: false);
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var provider = services.BuildServiceProvider();
            var changes = provider.GetRequiredService<PluginChangeStore>();
            var change = changes.Find(PluginReloadHarness.PluginId)!;
            change.Result.Should().Be(PluginChangeResult.Applied);
            change.FromVersion.Should().Be("1.0.0");
            change.ToVersion.Should().Be("1.1.0");
            Directory.Exists(Path.Combine(root, ".previous", PluginReloadHarness.PluginId)).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// At startup a fresh install that does not load ends cleanly: the plugin is taken out again and left uninstalled with no
    /// folder, no refusal and no record left behind, the failure is recorded with its reason and no restored version, and
    /// nothing is retried, so the host cannot loop.
    /// </summary>
    [Fact]
    public void Startup_AFreshInstallThatDoesNotLoad_IsLeftUninstalledAndRecordedAsFailed()
    {
        var root = PluginSigningTestSeeds.NewFolder("startup-fresh-failed");
        try
        {
            WriteBuilt(Path.Combine(root, PluginReloadHarness.PluginId), "1.0.0", broken: true);
            PluginFreshInstalls.Mark(root, PluginReloadHarness.PluginId);
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<PluginRegistry>();
            var changes = provider.GetRequiredService<PluginChangeStore>();
            registry.IsLoaded(PluginReloadHarness.PluginId).Should().BeFalse();
            registry.GetRefusedPlugins().Should().BeEmpty("an uninstalled plugin has no refusal to show");
            Directory.Exists(Path.Combine(root, PluginReloadHarness.PluginId)).Should().BeFalse("nothing of the failed install stays on disk");
            PluginFreshInstalls.IsPending(root, PluginReloadHarness.PluginId).Should().BeFalse("the first load has ended");
            var change = changes.Find(PluginReloadHarness.PluginId)!;
            change.Result.Should().Be(PluginChangeResult.Failed);
            change.FromVersion.Should().BeNull();
            change.ToVersion.Should().Be("1.0.0");
            change.RestoredVersion.Should().BeNull("a fresh install has nothing to restore");
            change.Error.Should().NotBeNullOrWhiteSpace();
            changes.Unreported().Should().ContainSingle(c => c.PluginId == PluginReloadHarness.PluginId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>At startup a fresh install that loads is final: the record of the wait goes and the change is recorded as applied with no earlier version.</summary>
    [Fact]
    public void Startup_AFreshInstallThatLoads_IsRecordedAsAppliedAndSettled()
    {
        var root = PluginSigningTestSeeds.NewFolder("startup-fresh-applied");
        try
        {
            WriteBuilt(Path.Combine(root, PluginReloadHarness.PluginId), "1.0.0", broken: false);
            PluginFreshInstalls.Mark(root, PluginReloadHarness.PluginId);
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<PluginRegistry>().IsLoaded(PluginReloadHarness.PluginId).Should().BeTrue();
            var change = provider.GetRequiredService<PluginChangeStore>().Find(PluginReloadHarness.PluginId)!;
            change.Result.Should().Be(PluginChangeResult.Applied);
            change.FromVersion.Should().BeNull();
            change.ToVersion.Should().Be("1.0.0");
            PluginFreshInstalls.IsPending(root, PluginReloadHarness.PluginId).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A plugin that was loaded before and is not a fresh install is not touched when it fails to load: it stays, refused, for the admin to fix or remove.</summary>
    [Fact]
    public void Startup_AnOrdinaryPluginThatDoesNotLoad_StaysOnDiskRefused()
    {
        var root = PluginSigningTestSeeds.NewFolder("startup-not-fresh");
        try
        {
            WriteBuilt(Path.Combine(root, PluginReloadHarness.PluginId), "1.0.0", broken: true);
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<PluginRegistry>().GetRefusedPlugins().Should().ContainSingle(r => r.PluginId == PluginReloadHarness.PluginId);
            Directory.Exists(Path.Combine(root, PluginReloadHarness.PluginId)).Should().BeTrue();
            provider.GetRequiredService<PluginChangeStore>().Find(PluginReloadHarness.PluginId).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Writes a plugin folder of the harness's shape: a manifest and an assembly that is loadable unless broken.</summary>
    /// <param name="folder">The folder to create.</param>
    /// <param name="version">The manifest version.</param>
    /// <param name="broken">Whether the assembly is not a loadable assembly.</param>
    private static void WriteBuilt(string folder, string version, bool broken)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson(PluginReloadHarness.PluginId, type: "App")
                .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal)
                .Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\"", StringComparison.Ordinal));
        File.WriteAllBytes(
            Path.Combine(folder, "Fixture.Reload.dll"),
            broken ? [0x4d, 0x5a, 0x00, 0x01] : PluginAssemblyFixtures.AppPlugin("Fixture.Reload", "Fixture.AppPlugin"));
    }

    /// <summary>Takes a weak reference to the load context of the loaded plugin, in a frame of its own so no local keeps it alive.</summary>
    /// <param name="harness">The harness whose plugin is loaded.</param>
    /// <returns>The weak reference.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WeakContextOf(PluginReloadHarness harness)
    {
        return new WeakReference(harness.Registry.Find(PluginReloadHarness.PluginId)!.LoadContext);
    }

    /// <summary>Runs the collector until the weak reference dies or the attempts run out.</summary>
    /// <param name="reference">The weak reference to a load context.</param>
    /// <returns><see langword="true"/> when the load context was collected.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsCollected(WeakReference reference)
    {
        for (var attempt = 0; attempt < 20 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !reference.IsAlive;
    }
}
