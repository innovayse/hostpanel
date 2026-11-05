using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Hostpanel.Sdk.Plugins.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Runs the migration hook (<see cref="IPluginMigrations"/>) of every plugin whose upgrade was swapped in and whose hook
/// has not succeeded yet, once per success. It runs while the host is starting, before the web server accepts a request,
/// and again for one plugin when the operator switches it on or saves its settings.
/// </summary>
/// <remarks>
/// <para>
/// "Once" rests on the pending record <see cref="PluginUpgradeApplier"/> writes before the swap: the hook runs, the
/// migration is written to the activity log, and only then is the record deleted and the plugin's gate lifted.
/// </para>
/// <para>
/// <b>Gate and retry.</b> While a plugin's record exists, <see cref="PluginRegistry.IsMigrationPending"/> is set and
/// <see cref="PluginActivator"/> refuses the plugin, so no call and no hosted start runs new code on old data. A hook
/// that throws or exceeds <see cref="PluginOptions.MigrationTimeoutSeconds"/>, a plugin that is switched off or lacks a
/// required setting, and a plugin that did not load all keep the record and the gate. The hook is tried again at the next
/// start and whenever the plugin is enabled or its settings are saved (<see cref="RetryAsync"/>); the SDK promises the
/// hook is idempotent. A plugin without the hook has its record deleted and the upgrade audited without a call.
/// </para>
/// <para>
/// The hook takes no versions, so the old and new versions are written to the log and the activity log line instead.
/// A plugin's failure is logged with its id and never stops the host or the other plugins. A hook that ignores its
/// cancellation token keeps running in the background after the timeout; the host stops waiting for it all the same.
/// </para>
/// </remarks>
public sealed class PluginMigrationRunner : IHostedLifecycleService, IPluginMigrationRetry
{
    /// <summary>Registry of the plugins loaded at startup, which also holds the migration gate of each.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Opens the scope a plugin is activated in and the audit line is saved in.</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>The upload folder, where the pending records live.</summary>
    private readonly string _installRoot;

    /// <summary>The longest one hook may run before the host stops waiting for it.</summary>
    private readonly TimeSpan _timeout;

    /// <summary>Records each migration, and each plugin whose migration failed or waits.</summary>
    private readonly ILogger<PluginMigrationRunner> _logger;

    /// <summary>Lets one migration attempt run at a time, so a startup run and a settings save cannot run one hook twice at once.</summary>
    private readonly SemaphoreSlim _attempt = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="PluginMigrationRunner"/> class.</summary>
    /// <param name="registry">Registry of the plugins loaded at startup, which also holds the migration gate of each.</param>
    /// <param name="scopeFactory">Opens the scope a plugin is activated in and the audit line is saved in.</param>
    /// <param name="options">The upload folder and the hook time limit.</param>
    /// <param name="logger">Records each migration, and each plugin whose migration failed or waits.</param>
    public PluginMigrationRunner(
        PluginRegistry registry,
        IServiceScopeFactory scopeFactory,
        IOptions<PluginOptions> options,
        ILogger<PluginMigrationRunner> logger)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
        _installRoot = options.Value.InstallRoot;
        _timeout = TimeSpan.FromSeconds(options.Value.MigrationTimeoutSeconds);
        _logger = logger;
    }

    /// <summary>Runs the pending migrations before any hosted service starts.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting; handed to each hook.</param>
    /// <returns>A task that completes when every pending migration ran or was left for a later attempt.</returns>
    public Task StartingAsync(CancellationToken cancellationToken) => RunPendingAsync(cancellationToken);

    /// <summary>No-op: the migrations ran in <see cref="StartingAsync"/>.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Runs the hook of every plugin with a pending upgrade record, one at a time, each failure isolated to its plugin.
    /// </summary>
    /// <param name="ct">Cancelled when the host gives up waiting; handed to each hook.</param>
    /// <returns>A task that completes when every record was handled.</returns>
    public async Task RunPendingAsync(CancellationToken ct)
    {
        await _attempt.WaitAsync(ct);
        try
        {
            foreach (var marker in PluginUpgradeApplier.ReadPending(_installRoot, _logger))
            {
                await MigrateGuardedAsync(marker, ct);
            }
        }
        finally
        {
            _attempt.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RetryAsync(string pluginId, CancellationToken ct)
    {
        if (!_registry.IsMigrationPending(pluginId))
        {
            return;
        }

        await _attempt.WaitAsync(ct);
        try
        {
            // Read under the lock: a concurrent attempt may just have finished this plugin.
            var marker = PluginUpgradeApplier.ReadPending(_installRoot, _logger).FirstOrDefault(m => m.PluginId == pluginId);
            if (marker is null)
            {
                _registry.ClearMigrationPending(pluginId);
                return;
            }

            await MigrateGuardedAsync(marker, ct);
        }
        finally
        {
            _attempt.Release();
        }
    }

    /// <summary>
    /// Migrates one plugin and updates its record and gate by the result. A failure or a timeout is logged and keeps the
    /// record and the gate; only the host giving up (cancellation) is rethrown.
    /// </summary>
    /// <param name="marker">The pending record.</param>
    /// <param name="ct">Cancellation token handed to the hook.</param>
    /// <returns>A task that completes when the attempt ended.</returns>
    private async Task MigrateGuardedAsync(PluginUpgradeMarker marker, CancellationToken ct)
    {
        try
        {
            if (await TryMigrateAsync(marker, ct))
            {
                PluginUpgradeApplier.DeleteMarker(_installRoot, marker.PluginId);
                _registry.ClearMigrationPending(marker.PluginId);
            }
            else
            {
                _registry.MarkMigrationPending(marker.PluginId);
            }
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            _registry.MarkMigrationPending(marker.PluginId);
            _logger.LogError(
                "Migration of plugin {Id} from {From} to {To} exceeded {Seconds} s and was abandoned, the plugin stays switched off until it succeeds",
                marker.PluginId, marker.FromVersion, marker.ToVersion, _timeout.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Plugin code and the audit save both run here; neither may stop the host. The record and the gate stay.
            _registry.MarkMigrationPending(marker.PluginId);
            _logger.LogError(
                ex, "Migration of plugin {Id} from {From} to {To} failed, the plugin stays switched off until it succeeds",
                marker.PluginId, marker.FromVersion, marker.ToVersion);
        }
    }

    /// <summary>Runs one plugin's hook, when it has one and may run, and audits the migration.</summary>
    /// <param name="marker">The pending record.</param>
    /// <param name="ct">Cancellation token handed to the hook.</param>
    /// <returns><see langword="true"/> when the migration is done and audited, so the record can go; <see langword="false"/> when it must be tried again.</returns>
    /// <exception cref="TimeoutException">The hook did not finish within the time limit.</exception>
    private async Task<bool> TryMigrateAsync(PluginUpgradeMarker marker, CancellationToken ct)
    {
        var plugin = _registry.Find(marker.PluginId);
        if (plugin is null)
        {
            // Not loaded: refused, or removed again since the swap. A removed plugin has nothing to migrate.
            var removed = !Directory.Exists(Path.Combine(_installRoot, marker.PluginId));
            _logger.LogWarning(
                "Plugin {Id} was upgraded to {To} but is not loaded: {Outcome}", marker.PluginId, marker.ToVersion,
                removed ? "its package is gone, dropping the migration" : "migration waits for the next start");
            return removed;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        if (typeof(IPluginMigrations).IsAssignableFrom(plugin.ImplementationType))
        {
            var activation = await scope.ServiceProvider.GetRequiredService<PluginActivator>()
                .ActivateForMigrationAsync<IPluginMigrations>(plugin, ct);
            if (activation.Outcome != PluginActivationOutcome.Activated)
            {
                _logger.LogInformation(
                    "Plugin {Id} was upgraded to {To}; its migration waits until it is enabled and configured ({Outcome})",
                    marker.PluginId, marker.ToVersion, activation.Outcome);
                return false;
            }

            // The token asks the hook to stop; WaitAsync makes the host stop waiting even for a hook that never looks at it.
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(_timeout);
            await activation.Instance!.MigrateAsync(limit.Token).WaitAsync(_timeout, ct);
        }

        await scope.ServiceProvider.GetRequiredService<IActivityLogger>().RecordAsync(
            new ActivityLogEntry(
                null, $"Migrated Plugin Data - Plugin ID: {marker.PluginId} - Version: {marker.FromVersion} to {marker.ToVersion}"),
            ct);
        await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(ct);
        return true;
    }
}
