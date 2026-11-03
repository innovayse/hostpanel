using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The upgrade swap and the migration hook: a staged upgrade replaces the installed plugin when the host starts, the
/// plugin's <c>IPluginMigrations</c> hook then runs exactly once and is audited, and a hook that throws or a plugin
/// that is switched off keeps the upgrade pending for the next start.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMigrationRunnerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The swap installs the staged version and the hook then runs once, however often the runner runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SwappedUpgrade_RunsTheHookOnceAndAuditsItAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("migrating");
            await EnableAsync(id);
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var runner = Runner(root, PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(MigratingPlugin)));

            await runner.RunPendingAsync(CancellationToken.None);
            await runner.RunPendingAsync(CancellationToken.None);

            PluginUpgradeApplier.ReadVersion(Path.Combine(root, id)).Should().Be("2.0.0", "the swap replaced the installed files");
            Directory.Exists(Path.Combine(root, ".staged", id)).Should().BeFalse("the staged folder was moved into place");
            MigratingPlugin.CallsOf(id).Should().Be(1, "the pending record is deleted once the migration is audited");
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().BeEmpty();
            (await MigrationLinesAsync(id)).Should().ContainSingle().Which.Should().Contain("1.0.0 to 2.0.0");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A hook that throws is isolated: the runner does not throw and the upgrade stays pending.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FailingHook_KeepsTheUpgradePendingAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("failing-migration");
            await EnableAsync(id);
            Stage(root, id, "1.0.0", "1.1.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var runner = Runner(root, PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(FailingMigratingPlugin)));

            var run = () => runner.RunPendingAsync(CancellationToken.None);

            await run.Should().NotThrowAsync();
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle(m => m.PluginId == id);
            (await MigrationLinesAsync(id)).Should().BeEmpty("a failed migration is not audited as done");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A plugin that is switched off is not called; its migration waits until it is enabled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledPlugin_WaitsWithoutRunningTheHookAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("disabled-migration");
            Stage(root, id, "1.0.0", "1.0.1");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var runner = Runner(root, PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(MigratingPlugin)));

            await runner.RunPendingAsync(CancellationToken.None);

            MigratingPlugin.CallsOf(id).Should().Be(0);
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle(m => m.PluginId == id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>With no staged upgrade the swap changes nothing.</summary>
    [Fact]
    public void NoStagedUpgrade_SwapsNothing()
    {
        var root = NewInstallRoot();
        try
        {
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);

            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The record is written before the folders move: when it cannot be written, nothing is swapped. Written after the
    /// swap, the plugin would already be replaced and its migration unrecorded.
    /// </summary>
    [Fact]
    public void Swap_WritesTheRecordBeforeMovingAnyFolder()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("record-first");
            Stage(root, id, "1.0.0", "2.0.0");
            File.WriteAllText(Path.Combine(root, ".upgrades"), "a file where the record folder must be");

            PluginUpgradeApplier.Apply(root, NullLogger.Instance);

            PluginUpgradeApplier.ReadVersion(Path.Combine(root, id)).Should().Be("1.0.0", "the swap did not happen without its record");
            Directory.Exists(Path.Combine(root, ".staged", id)).Should().BeTrue("the staged upgrade waits for the next start");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A successful swap leaves a complete record and no half-written temporary file.</summary>
    [Fact]
    public void Swap_LeavesACompleteRecordAndNoTemporaryFile()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("atomic-record");
            Stage(root, id, "1.0.0", "2.0.0");

            PluginUpgradeApplier.Apply(root, NullLogger.Instance);

            var pending = PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle().Subject;
            pending.Should().Be(new PluginUpgradeMarker(id, "1.0.0", "2.0.0"));
            Directory.GetFiles(Path.Combine(root, ".upgrades"), "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A record that cannot be read is logged with its file name and still gates and migrates the plugin it names.</summary>
    [Fact]
    public void TruncatedRecord_IsLoggedWithItsFileName()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("truncated");
            WritePlugin(Path.Combine(root, id), id, "2.0.0");
            Directory.CreateDirectory(Path.Combine(root, ".upgrades"));
            File.WriteAllText(Path.Combine(root, ".upgrades", $"{id}.json"), "{\"pluginId\":\"" + id[..4]);
            var logs = new RecordingLoggerProvider();

            var pending = PluginUpgradeApplier.ReadPending(root, logs.CreateLogger("test"));

            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains($"{id}.json"));
            pending.Should().ContainSingle().Which.PluginId.Should().Be(id, "the file name still says which plugin is waiting");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A second upgrade on top of one whose hook never succeeded keeps the first FromVersion.</summary>
    [Fact]
    public void SecondUpgrade_KeepsTheFirstFromVersion()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("keep-from");
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            WritePlugin(Path.Combine(root, ".staged", id), id, "3.0.0");

            PluginUpgradeApplier.Apply(root, NullLogger.Instance);

            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle()
                .Which.Should().Be(new PluginUpgradeMarker(id, "1.0.0", "3.0.0"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Stale work folders are swept at startup, and a version left set aside by an interrupted swap is moved back.</summary>
    [Fact]
    public void Startup_SweepsStaleWorkFoldersAndRecoversAnInterruptedSwap()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("interrupted");
            WritePlugin(Path.Combine(root, ".staging", $"previous-{id}"), id, "1.0.0");
            WritePlugin(Path.Combine(root, ".staging", "0123456789abcdef"), "leftover", "1.0.0");
            WritePlugin(Path.Combine(root, ".staged", id), id, "2.0.0");

            PluginUpgradeApplier.Apply(root, NullLogger.Instance);

            Directory.Exists(Path.Combine(root, ".staging", "0123456789abcdef")).Should().BeFalse("stale work folders are swept");
            Directory.GetDirectories(Path.Combine(root, ".staging")).Should().BeEmpty();
            PluginUpgradeApplier.ReadVersion(Path.Combine(root, id)).Should().Be("2.0.0", "the interrupted swap was recovered, then finished");
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle()
                .Which.FromVersion.Should().Be("1.0.0", "the version set aside is the one that was replaced");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A plugin with a pending migration is not activated, and is activated once its hook succeeds, so no call reaches
    /// new code on old data.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingMigration_GatesThePluginUntilTheHookSucceedsAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("gated");
            await EnableAsync(id);
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var plugin = PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(MigratingPlugin));
            var registry = PluginActivationTestSeeds.Registry(plugin);
            registry.MarkMigrationPending(id);

            (await PrepareOutcomeAsync(registry, plugin)).Should().Be(PluginActivationOutcome.MigrationPending);

            await Runner(root, registry).RunPendingAsync(CancellationToken.None);

            registry.IsMigrationPending(id).Should().BeFalse();
            (await PrepareOutcomeAsync(registry, plugin)).Should().Be(PluginActivationOutcome.Activated);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A hook that throws leaves the plugin gated: the failure is part of the gate, not just of the log.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FailedMigration_KeepsThePluginGatedAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("gated-failure");
            await EnableAsync(id);
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var plugin = PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(FailingMigratingPlugin));
            var registry = PluginActivationTestSeeds.Registry(plugin);

            await Runner(root, registry).RunPendingAsync(CancellationToken.None);

            registry.IsMigrationPending(id).Should().BeTrue();
            (await PrepareOutcomeAsync(registry, plugin)).Should().Be(PluginActivationOutcome.MigrationPending);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A hook that hangs is abandoned after the time limit: startup goes on, the record and the gate stay.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HangingHook_TimesOutAndKeepsTheUpgradePendingAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("hanging");
            await EnableAsync(id);
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var registry = PluginActivationTestSeeds.Registry(PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(HangingMigratingPlugin)));
            var runner = Runner(root, registry, timeoutSeconds: 1);

            var run = () => runner.RunPendingAsync(CancellationToken.None);

            await run.Should().CompleteWithinAsync(TimeSpan.FromSeconds(30));
            registry.IsMigrationPending(id).Should().BeTrue();
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().ContainSingle(m => m.PluginId == id);
            (await MigrationLinesAsync(id)).Should().BeEmpty("an abandoned migration is not audited as done");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A plugin that was switched off at startup runs its hook as soon as the operator enables it, and the gate lifts.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RetryAfterEnabling_RunsTheHookAndLiftsTheGateAsync()
    {
        var root = NewInstallRoot();
        try
        {
            var id = PluginActivationTestSeeds.NewId("retry");
            Stage(root, id, "1.0.0", "2.0.0");
            PluginUpgradeApplier.Apply(root, NullLogger.Instance);
            var registry = PluginActivationTestSeeds.Registry(PluginActivationTestSeeds.Plugin(id, PluginType.App, typeof(MigratingPlugin)));
            registry.MarkMigrationPending(id);
            var runner = Runner(root, registry);
            await runner.RunPendingAsync(CancellationToken.None);
            MigratingPlugin.CallsOf(id).Should().Be(0, "it is switched off");

            await EnableAsync(id);
            await runner.RetryAsync(id, CancellationToken.None);

            MigratingPlugin.CallsOf(id).Should().Be(1);
            registry.IsMigrationPending(id).Should().BeFalse();
            PluginUpgradeApplier.ReadPending(root, NullLogger.Instance).Should().BeEmpty();
            (await MigrationLinesAsync(id)).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Creates an empty upload folder for one test.</summary>
    /// <returns>The absolute folder path; the test deletes it.</returns>
    private static string NewInstallRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"plugin-upgrade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Writes an installed plugin of one version and a staged upgrade of another.</summary>
    /// <param name="root">The upload folder.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="installed">The installed version.</param>
    /// <param name="staged">The staged version.</param>
    private static void Stage(string root, string id, string installed, string staged)
    {
        WritePlugin(Path.Combine(root, id), id, installed);
        WritePlugin(Path.Combine(root, ".staged", id), id, staged);
    }

    /// <summary>Writes a plugin folder holding only a manifest.</summary>
    /// <param name="folder">The folder to create.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="version">The version to declare.</param>
    private static void WritePlugin(string folder, string id, string version)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson(id).Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\""));
    }

    /// <summary>Stores the plugin's switch as on.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    private async Task EnableAsync(string id)
    {
        using var scope = factory.Services.CreateScope();
        await PluginActivationTestSeeds.EnableAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), id);
    }

    /// <summary>Builds the runner over the real host's scopes and a registry holding <paramref name="plugins"/>.</summary>
    /// <param name="root">The upload folder the runner reads pending upgrades from.</param>
    /// <param name="plugins">The plugins the registry reports as loaded.</param>
    /// <returns>The runner.</returns>
    private PluginMigrationRunner Runner(string root, params LoadedPlugin[] plugins)
    {
        return Runner(root, PluginActivationTestSeeds.Registry(plugins));
    }

    /// <summary>Builds the runner over the real host's scopes and a registry the test keeps hold of.</summary>
    /// <param name="root">The upload folder the runner reads pending upgrades from.</param>
    /// <param name="registry">The registry the runner updates and the test reads.</param>
    /// <param name="timeoutSeconds">The time limit of one hook.</param>
    /// <returns>The runner.</returns>
    private PluginMigrationRunner Runner(string root, PluginRegistry registry, int timeoutSeconds = PluginOptions.DefaultMigrationTimeoutSeconds)
    {
        return new PluginMigrationRunner(
            registry,
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PluginOptions { InstallPath = root, MigrationTimeoutSeconds = timeoutSeconds }),
            NullLogger<PluginMigrationRunner>.Instance);
    }

    /// <summary>Asks the activator, over the given registry, whether a plugin would be activated for its migration contract.</summary>
    /// <param name="registry">The registry holding the migration gate.</param>
    /// <param name="plugin">The plugin.</param>
    /// <returns>The activation outcome.</returns>
    private async Task<PluginActivationOutcome> PrepareOutcomeAsync(PluginRegistry registry, LoadedPlugin plugin)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prepared = await PluginActivationTestSeeds.Activator(factory, db, null, registry)
            .PrepareAsync<IPluginMigrations>(plugin, CancellationToken.None);
        return prepared.Outcome;
    }

    /// <summary>Reads the activity log lines that record a plugin's data migration.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>The descriptions, oldest first.</returns>
    private async Task<List<string>> MigrationLinesAsync(string id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.StartsWith("Migrated Plugin Data") && l.Description.Contains(id))
            .Select(l => l.Description)
            .ToListAsync();
    }
}
