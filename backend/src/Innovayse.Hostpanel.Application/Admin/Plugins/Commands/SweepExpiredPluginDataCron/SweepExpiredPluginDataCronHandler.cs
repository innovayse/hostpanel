using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SweepExpiredPluginDataCron;

/*
 * An expired value is already absent for the plugin and is not counted against its key limit when a write needs the room,
 * so this sweep is housekeeping: it frees the rows and brings each scope's totals back to what the live rows hold.
 *
 * Rows are removed through their scope, as the purge of a removed plugin does it: each removed entry is released from its
 * PluginDataScope, which moves the scope's revision, so the scope's xmin token serializes the sweep against a write of the
 * same scope. Whichever saves second meets ConcurrencyConflictException; the sweep detaches its batch and reads the scope
 * again, which also lets it see a value the plugin has just written again with a new time to live. A scope row stays when
 * it is empty: the next write of that plugin reuses it.
 *
 * A scope whose sweep fails is logged with its plugin id and skipped, so the others still run; the failed batch is detached
 * and the next pass retries the scope. The pass returns normally, so RecurringJobs schedules the next one.
 *
 * Rows are loaded and removed with Remove + SaveChangesAsync, never ExecuteDelete: the architecture rules ban bulk deletes
 * in handlers because they skip the interceptors.
 */

/// <summary>
/// Scheduled handler that removes the expired values of the plugin store, scope by scope. A recurring background message,
/// not dispatched by a caller, so it returns <see cref="Task"/>; a pass is idempotent, a scope that fails is logged and
/// skipped so the others still run, and the next pass retries it.
/// </summary>
public sealed class SweepExpiredPluginDataCronHandler
{
    /// <summary>
    /// How many rows one batch loads tracked, removes and saves. A scope holds at most the configured key cap, so 500
    /// keeps one batch's change tracker and its statements small while needing few round trips.
    /// </summary>
    private const int BatchSize = 500;

    /// <summary>The Plugins module's data context, owner of the plugin store tables.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that tells which values have expired.</summary>
    private readonly IClock _clock;

    /// <summary>Runs the pass under its lease and schedules the next one.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Records a batch lost to a concurrent write and the end of each pass.</summary>
    private readonly ILogger<SweepExpiredPluginDataCronHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The Plugins module's data context, owner of the plugin store tables.</param>
    /// <param name="clock">The clock that tells which values have expired.</param>
    /// <param name="jobs">Runs the pass under its lease and schedules the next one.</param>
    /// <param name="logger">Records a batch lost to a concurrent write and the end of each pass.</param>
    public SweepExpiredPluginDataCronHandler(
        IHostpanelDbContext context, IClock clock, RecurringJobs jobs, ILogger<SweepExpiredPluginDataCronHandler> logger)
    {
        _context = context;
        _clock = clock;
        _jobs = jobs;
        _logger = logger;
    }

    /// <summary>Handles the command: runs one pass through <see cref="RecurringJobs"/>, which schedules the next.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the pass has run and the next one is scheduled.</returns>
    public Task HandleAsync(SweepExpiredPluginDataCronCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return _jobs.RunAsync<SweepExpiredPluginDataCronCommand>(
            _jobs.Schedules.SweepExpiredPluginData, () => RunAsync(ct), ct);
    }

    /// <summary>One pass: finds the scopes that hold an expired value and sweeps each; a scope that fails is logged and skipped.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once no expired value of the moment the pass began is left.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        // One moment for the whole pass, so a value that expires while it runs is left for the next one.
        var now = _clock.UtcNow;
        var scopes = await _context.PluginDataEntries
            .AsNoTracking()
            .Where(e => e.ExpiresAt != null && e.ExpiresAt <= now)
            .Select(e => new { e.PluginId, e.ClientRef })
            .Distinct()
            .ToListAsync(ct);

        var deleted = 0;
        var failed = 0;
        foreach (var scope in scopes)
        {
            try
            {
                deleted += await SweepScopeAsync(scope.PluginId, scope.ClientRef, now, ct);
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                // The failed batch is still tracked and would ride along with, and fail, the next scope's save;
                // detaching it isolates this scope, and the next pass retries it.
                _context.DetachAll();
                failed++;
                _logger.LogError(ex, "Sweeping the expired plugin store entries of plugin {PluginId} failed.", scope.PluginId);
            }
        }

        _logger.LogInformation(
            "Swept {EntryCount} expired plugin store entries in {ScopeCount} store scopes, {FailedCount} failed.",
            deleted, scopes.Count, failed);
    }

    /// <summary>Removes one scope's expired entries, batch by batch.</summary>
    /// <param name="pluginId">The plugin of the scope.</param>
    /// <param name="clientRef">The client reference of the scope, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <param name="now">The moment the pass began; entries expiring at or before it are removed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many entries this pass deleted from the scope.</returns>
    private async Task<int> SweepScopeAsync(string pluginId, string? clientRef, DateTimeOffset now, CancellationToken ct)
    {
        var deleted = 0;
        while (true)
        {
            var scope = await _context.PluginDataScopes
                .FirstOrDefaultAsync(s => s.PluginId == pluginId && s.ClientRef == clientRef, ct);
            if (scope is null)
            {
                // The plugin's data was purged meanwhile.
                return deleted;
            }

            var batch = await _context.PluginDataEntries
                .Where(e => e.PluginId == pluginId && e.ClientRef == clientRef)
                .Where(e => e.ExpiresAt != null && e.ExpiresAt <= now)
                .OrderBy(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0)
            {
                _context.DetachAll();
                return deleted;
            }

            foreach (var entry in batch)
            {
                scope.Release(entry.StoredChars);
                _context.PluginDataEntries.Remove(entry);
            }

            if (await TrySaveBatchAsync(pluginId, ct))
            {
                deleted += batch.Count;
                if (batch.Count < BatchSize)
                {
                    return deleted;
                }
            }
        }
    }

    /// <summary>
    /// Saves one staged batch with its scope. A concurrency conflict means a write of the plugin changed the scope first:
    /// everything staged is detached, so the next round reads the scope again.
    /// </summary>
    /// <param name="pluginId">The plugin of the scope, for the log line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the batch was saved; <see langword="false"/> when it lost the race.</returns>
    private async Task<bool> TrySaveBatchAsync(string pluginId, CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            // The whole batch rolled back with the conflict, so nothing of it is assumed deleted. The context holds
            // nothing else: this handler stages only its own batch.
            _context.DetachAll();
            _logger.LogInformation(
                "A sweep batch of plugin {PluginId} met a concurrent write; reading the scope again.", pluginId);
            return false;
        }
    }
}
