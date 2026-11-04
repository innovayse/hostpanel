using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.SDK.Plugins.Store;
using Microsoft.EntityFrameworkCore;

/*
 * The store is written through the request's own data context, like every other write: the rows are loaded tracked,
 * changed through PluginDataEntry and PluginDataScope, and saved once, so the timestamps come from the auditable
 * interceptor. Saving that context flushes only the store's own change because PluginCallRunner saves its audit line
 * before it builds the plugin, so nothing else is staged on the context while plugin code runs.
 *
 * Each scope (one plugin and one client reference, or the plugin-wide scope) has a PluginDataScope row holding its key
 * count and its size, and every write is checked against the PluginStoreQuota there. Every write also moves that row's
 * revision, so the row is updated by every write of the scope and its xmin concurrency token makes two writes of one
 * scope that ran at the same time conflict on save: exactly one commits, the other is rolled back whole (its entry change
 * with it) and retried from a fresh read. That is what makes the quota exact, where a count taken before an insert
 * would let two new keys both pass, and what keeps the totals equal to the rows when two writes race. The first write of
 * a scope or of a key races on the unique indexes instead, which AppDbContext translates to the same
 * ConcurrencyConflictException, so it takes the same retry.
 *
 * Every operation starts from rows read in that operation and detaches what it tracked when it ends, committed or not:
 * a retry must not see the totals of the attempt that lost, and a refused write must leave nothing staged that a later
 * save would flush. On a retry the values are last writer wins; a Remove that finds its key already gone answers false
 * (the other call removed it, so it is absent), and a Set that finds it gone creates it again.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The SDK's <see cref="IPluginStore"/> for one call, bound when it is built to the plugin and the plugin's opaque
/// reference of the call's client: per client on every client area call (and on an admin call once it carries a
/// verified client), plugin wide (no reference) on an admin call that is not about a client. Every query filters on
/// both and no method takes a scope, so a plugin reads and writes only its own keys of the client its call is bound to.
/// A scope holds at most the <see cref="PluginStoreQuota"/> it is given, exactly, however many calls write it at once: a
/// write that would grow the key count or the size past it throws <see cref="DomainRuleException"/> with
/// <see cref="PluginStoreErrors.QuotaExceeded"/> after raising the call's <see cref="PluginStoreQuotaSignal"/>; a write
/// that shrinks or keeps the size, and a removal, never is refused. A key or value that breaks the contract throws
/// <see cref="DomainRuleException"/> from <see cref="PluginDataEntry"/>, which the call runner answers as a plugin failure.
/// </summary>
internal sealed class PluginStore : IPluginStore
{
    /// <summary>
    /// How many times one write is tried before a lost race is let through as <see cref="ConcurrencyConflictException"/>.
    /// Only writes of the same scope race (one plugin's data about one client), so two calls at once is the realistic
    /// worst case and a few attempts cover a burst; past that the call fails rather than loop.
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>The plugin whose values these are.</summary>
    private readonly string _pluginId;

    /// <summary>The plugin's opaque reference of the call's client, or <see langword="null"/> for plugin-wide values.</summary>
    private readonly string? _clientRef;

    /// <summary>The request's data context; the store's writes are saved through it.</summary>
    private readonly AppDbContext _context;

    /// <summary>What this store's scope may hold; every write is checked against it on the scope's totals row.</summary>
    private readonly PluginStoreQuota _quota;

    /// <summary>The call's record of a quota refusal, raised before the refusal is rethrown so the runner can answer it.</summary>
    private readonly PluginStoreQuotaSignal _signal;

    /// <summary>Initializes the store of one call.</summary>
    /// <param name="pluginId">The plugin whose values these are.</param>
    /// <param name="clientRef">The plugin's opaque reference of the call's client, or <see langword="null"/> for plugin-wide values.</param>
    /// <param name="context">The request's data context; the store's writes are saved through it.</param>
    /// <param name="quota">What this store's scope may hold; every write is checked against it on the scope's totals row.</param>
    /// <param name="signal">The call's record of a quota refusal, raised before the refusal is rethrown so the runner can answer it.</param>
    public PluginStore(
        string pluginId, string? clientRef, AppDbContext context, PluginStoreQuota quota, PluginStoreQuotaSignal signal)
    {
        _pluginId = pluginId;
        _clientRef = clientRef;
        _context = context;
        _quota = quota;
        _signal = signal;
    }

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        PluginDataEntry.EnsureKey(key);
        return await Scoped(key).AsNoTracking().Select(row => row.Value).FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        // Both rules first, so a broken key or value is refused before anything is read and is never measured.
        PluginDataEntry.EnsureKey(key);
        PluginDataEntry.EnsureValue(value);
        try
        {
            await WriteAsync((tracked, token) => SetOnceAsync(key, value, tracked, token), ct);
        }
        catch (DomainRuleException ex) when (ex.Code == PluginStoreErrors.QuotaExceeded)
        {
            // Raised here, by the host, and never by the plugin: a plugin that catches the refusal still cannot hide
            // from the runner that its write was refused, and cannot make the host answer with the quota code.
            _signal.Raise();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string key, CancellationToken ct)
    {
        PluginDataEntry.EnsureKey(key);
        return await WriteAsync((tracked, token) => RemoveOnceAsync(key, tracked, token), ct);
    }

    /// <summary>
    /// Runs one write, retrying it from a fresh read when it loses a race with another write of the same scope, and
    /// detaches what each attempt tracked when the attempt ends.
    /// </summary>
    /// <typeparam name="T">What the write answers.</typeparam>
    /// <param name="attempt">One attempt of the write; it adds every entity it tracks to the list it is given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the attempt that committed answered.</returns>
    /// <exception cref="ConcurrencyConflictException">Thrown when the write lost the race <see cref="MaxAttempts"/> times.</exception>
    private async Task<T> WriteAsync<T>(Func<List<object>, CancellationToken, Task<T>> attempt, CancellationToken ct)
    {
        for (var round = 1; ; round++)
        {
            List<object> tracked = [];
            try
            {
                return await attempt(tracked, ct);
            }
            catch (ConcurrencyConflictException) when (round < MaxAttempts)
            {
                // Another write of this scope committed first; this attempt's transaction was rolled back whole, so the
                // next round reads the totals and the entry again as that write left them.
            }
            finally
            {
                Detach(tracked);
            }
        }
    }

    /// <summary>One attempt of a write: the value of <paramref name="key"/> becomes <paramref name="value"/>.</summary>
    /// <param name="key">The key, already checked.</param>
    /// <param name="value">The value, already checked.</param>
    /// <param name="tracked">Receives every entity this attempt tracks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> once the write is saved.</returns>
    /// <exception cref="DomainRuleException">Thrown with <see cref="PluginStoreErrors.QuotaExceeded"/> when the write would pass the quota.</exception>
    /// <exception cref="ConcurrencyConflictException">Thrown when another write of the scope or the key committed first.</exception>
    private async Task<bool> SetOnceAsync(string key, string value, List<object> tracked, CancellationToken ct)
    {
        var scope = await LoadScopeAsync(tracked, ct);
        var size = PluginDataEntry.CharsOf(key, value);
        var existing = await Scoped(key).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            // The quota is asked before the row exists, so a refused key is never staged.
            scope.Admit(1, size, _quota);
            var created = PluginDataEntry.Create(_pluginId, _clientRef, key, value);
            _context.PluginDataEntries.Add(created);
            tracked.Add(created);
        }
        else
        {
            tracked.Add(existing);
            scope.Admit(0, size - existing.StoredChars, _quota);
            existing.ChangeValue(value);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>One attempt of a removal of <paramref name="key"/>.</summary>
    /// <param name="key">The key, already checked.</param>
    /// <param name="tracked">Receives every entity this attempt tracks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the key was there and is now removed; <see langword="false"/> when it was not there.</returns>
    /// <exception cref="ConcurrencyConflictException">Thrown when another write of the scope committed first.</exception>
    private async Task<bool> RemoveOnceAsync(string key, List<object> tracked, CancellationToken ct)
    {
        // The totals are read before the entry, never after: a removal that commits between the two reads would
        // otherwise pair an entry that is gone with totals that no longer count it. Read in this order, the entry is
        // never older than the totals, and totals that turn stale before the save meet the scope's xmin token.
        var scope = await LoadScopeAsync(tracked, ct);
        var existing = await Scoped(key).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return false;
        }

        tracked.Add(existing);
        scope.Release(existing.StoredChars);
        _context.PluginDataEntries.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Loads this store's scope totals tracked, or opens and stages them when the scope has never been written.</summary>
    /// <param name="tracked">Receives the scope row.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The scope row, tracked by the context.</returns>
    private async Task<PluginDataScope> LoadScopeAsync(List<object> tracked, CancellationToken ct)
    {
        var scope = await _context.PluginDataScopes
            .Where(row => row.PluginId == _pluginId && row.ClientRef == _clientRef)
            .FirstOrDefaultAsync(ct);
        if (scope is null)
        {
            scope = PluginDataScope.Open(_pluginId, _clientRef);
            _context.PluginDataScopes.Add(scope);
        }

        tracked.Add(scope);
        return scope;
    }

    /// <summary>Stops tracking what one attempt tracked, so neither its changes nor its stale totals outlive it.</summary>
    /// <param name="tracked">The entities the attempt tracked.</param>
    private void Detach(List<object> tracked)
    {
        foreach (var entity in tracked)
        {
            _context.Entry(entity).State = EntityState.Detached;
        }
    }

    /// <summary>The row of one key in this store's plugin and client scope.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The query; tracked unless the caller asks otherwise.</returns>
    private IQueryable<PluginDataEntry> Scoped(string key) => _context.PluginDataEntries
        .Where(row => row.PluginId == _pluginId && row.ClientRef == _clientRef && row.Key == key);
}
