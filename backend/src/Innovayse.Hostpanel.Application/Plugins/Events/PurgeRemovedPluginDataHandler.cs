using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Plugins.Events;

/*
 * Why this is a queued event handler that returns Task, why it purges scope by scope, and why it stops at the moment of
 * the removal.
 *
 * PluginDataPurgeRequestedEvent is written to the outbox in the same save that records the operator's purge
 * (PurgePluginDataHandler), so this purge runs if and only if the request committed. A removal alone never raises it:
 * the data is kept until an operator asks. Returning Task, not Result, is
 * what QueuedDelivery.IsQueued reads: the chain gets a durable inbox (DurableQueuePolicy) and the cooldown and
 * durable retries of DurableDeliveryRetryPolicy, so a database outage mid-purge is retried and survives a restart.
 * Exceptions are therefore left to propagate; catching them would turn a failed purge into a silent success.
 *
 * A retry can run hours after the removal, and by then the same plugin id may have been installed and switched on
 * again. So the purge deletes only the rows last written at or before the removal (OccurredAt): a row the new
 * installation wrote or rewrote is newer and is kept. The price is that a call already running when the switch went
 * off may still write a row after it, and that row is kept too; it is in the plugin's own scope and counted by it.
 *
 * Rows are removed through their scope: each removed entry is released from its PluginDataScope (so the totals keep
 * describing the rows) and the scope row is deleted once it holds nothing. Releasing changes the scope row, so its
 * xmin token serializes the purge against a write of the reinstalled plugin to the same scope: whichever saves second
 * meets ConcurrencyConflictException, and the purge detaches its batch and reads that scope again. The same signal
 * covers a second delivery deleting the same rows, so the handler is idempotent: a repeat finds nothing old and stops.
 *
 * Rows are loaded and removed with Remove + SaveChangesAsync, never ExecuteDelete: the architecture rules ban bulk
 * deletes in handlers because they skip the interceptors.
 */

/// <summary>
/// Purges what a removed plugin kept in the host's plugin store: in every scope of that plugin id, every
/// <c>PluginDataEntries</c> row last written at or before the removal, released from its <c>PluginDataScopes</c> row,
/// which is deleted once empty. Runs on a durable queue, is retried on failure and is idempotent.
/// </summary>
public sealed class PurgeRemovedPluginDataHandler
{
    /// <summary>
    /// How many rows one batch loads tracked, removes and saves. A scope may hold up to the configured key cap, so
    /// loading every row of a scope at once could be a very large tracked set; 500 keeps one batch's change tracker
    /// and its statements small while needing few round trips.
    /// </summary>
    private const int BatchSize = 500;

    /// <summary>The Plugins module's data context, owner of the plugin store tables.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records a batch lost to a concurrent write and the end of each purge.</summary>
    private readonly ILogger<PurgeRemovedPluginDataHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The Plugins module's data context, owner of the plugin store tables.</param>
    /// <param name="logger">Records a batch lost to a concurrent write and the end of each purge.</param>
    public PurgeRemovedPluginDataHandler(IHostpanelDbContext context, ILogger<PurgeRemovedPluginDataHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Deletes the removed plugin's store rows written up to the removal, scope by scope.</summary>
    /// <param name="evt">The removal, naming the plugin and the moment it was switched off.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once no row of the plugin from before the removal is left.</returns>
    public async Task HandleAsync(PluginDataPurgeRequestedEvent evt, CancellationToken ct)
    {
        var scopeIds = await _context.PluginDataScopes
            .AsNoTracking()
            .Where(s => s.PluginId == evt.PluginId)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var deleted = 0;
        foreach (var scopeId in scopeIds)
        {
            deleted += await PurgeScopeAsync(scopeId, evt.OccurredAt, ct);
        }

        _logger.LogInformation(
            "Purged {EntryCount} store entries in {ScopeCount} store scopes of removed plugin {PluginId}.",
            deleted, scopeIds.Count, evt.PluginId);
    }

    /// <summary>Removes one scope's entries written up to <paramref name="removedAt"/>, batch by batch, and the scope once empty.</summary>
    /// <param name="scopeId">The scope row's id.</param>
    /// <param name="removedAt">When the plugin was switched off; rows written later are kept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many entries this delivery deleted from the scope.</returns>
    private async Task<int> PurgeScopeAsync(int scopeId, DateTimeOffset removedAt, CancellationToken ct)
    {
        var deleted = 0;
        while (true)
        {
            var scope = await _context.PluginDataScopes.FirstOrDefaultAsync(s => s.Id == scopeId, ct);
            if (scope is null)
            {
                // Another delivery emptied and deleted it.
                return deleted;
            }

            var batch = await _context.PluginDataEntries
                .Where(e => e.PluginId == scope.PluginId && e.ClientRef == scope.ClientRef)
                .Where(e => (e.UpdatedAt ?? e.CreatedAt) <= removedAt)
                .OrderBy(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            foreach (var entry in batch)
            {
                scope.Release(entry.StoredChars);
                _context.PluginDataEntries.Remove(entry);
            }

            // Only an empty scope goes: one the reinstalled plugin wrote to keeps counting its newer rows.
            if (scope.KeyCount == 0)
            {
                _context.PluginDataScopes.Remove(scope);
            }

            if (batch.Count == 0 && scope.KeyCount != 0)
            {
                _context.DetachAll();
                return deleted;
            }

            if (await TrySaveBatchAsync(scope.PluginId, ct))
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
    /// Saves one staged batch with its scope. A concurrency conflict means another delivery or a write of the
    /// reinstalled plugin changed the scope first: everything staged is detached, so the next pass reads the scope again.
    /// </summary>
    /// <param name="pluginId">The removed plugin's id, for the log line.</param>
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
                "A purge batch of removed plugin {PluginId} met a concurrent change; reading the scope again.", pluginId);
            return false;
        }
    }
}
