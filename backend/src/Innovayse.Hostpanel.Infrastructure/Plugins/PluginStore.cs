using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.SDK.Plugins.Store;
using Microsoft.EntityFrameworkCore;

/*
 * The store is written through the request's own data context, like every other write: the row is loaded tracked,
 * changed through PluginDataEntry, and saved, so the timestamps come from the auditable interceptor. Saving that context
 * flushes only the store's own change because PluginCallRunner saves its audit line before it builds the plugin, so
 * nothing else is staged on the context while plugin code runs. Two first writes of one key race on the unique index,
 * which AppDbContext translates to ConcurrencyConflictException like every other lost race; the loser takes the
 * winner's row and writes over it once.
 *
 * A scope holds at most the configured number of keys (PluginApiOptions.MaxStoreKeysPerScope), so a plugin cannot grow
 * the table without bound. The count is taken only when a write would add a new key; updating an existing key never
 * checks it. The count and the insert are not one atomic step, so two concurrent new keys may each pass the count and
 * leave the scope one or two over the cap. That is accepted: the cap bounds growth, it is not a quota.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The SDK's <see cref="IPluginStore"/> for one call, bound when it is built to the plugin and the plugin's opaque
/// reference of the call's client: per client on every client area call (and on an admin call once it carries a
/// verified client), plugin wide (no reference) on an admin call that is not about a client. Every query filters on
/// both and no method takes a scope, so a plugin reads and writes only its own keys of the client its call is bound to.
/// A scope holds at most <c>maxKeys</c> keys: a write that would add a key beyond that is refused, an update of an
/// existing key never is, and concurrent first writes may leave the scope one or two over (the cap bounds growth, it is
/// not a quota). A key or value that breaks the contract, or a write over the cap, throws <see cref="DomainRuleException"/> from <see cref="PluginDataEntry"/>, which
/// the call runner answers as a plugin failure.
/// </summary>
internal sealed class PluginStore : IPluginStore
{
    /// <summary>The plugin whose values these are.</summary>
    private readonly string _pluginId;

    /// <summary>The plugin's opaque reference of the call's client, or <see langword="null"/> for plugin-wide values.</summary>
    private readonly string? _clientRef;

    /// <summary>The request's data context; the store's writes are saved through it.</summary>
    private readonly AppDbContext _context;

    /// <summary>The most keys the plugin may keep in this store's scope; a write that adds a key beyond it is refused.</summary>
    private readonly int _maxKeys;

    /// <summary>Initializes the store of one call.</summary>
    /// <param name="pluginId">The plugin whose values these are.</param>
    /// <param name="clientRef">The plugin's opaque reference of the call's client, or <see langword="null"/> for plugin-wide values.</param>
    /// <param name="context">The request's data context; the store's writes are saved through it.</param>
    /// <param name="maxKeys">The most keys the plugin may keep in this store's scope; a write that adds a key beyond it is refused.</param>
    public PluginStore(string pluginId, string? clientRef, AppDbContext context, int maxKeys)
    {
        _pluginId = pluginId;
        _clientRef = clientRef;
        _context = context;
        _maxKeys = maxKeys;
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
        PluginDataEntry.EnsureKey(key);
        var existing = await Scoped(key).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            existing.ChangeValue(value);
            await _context.SaveChangesAsync(ct);
            return;
        }

        var keysInScope = await Scope().AsNoTracking().CountAsync(ct);
        PluginDataEntry.EnsureRoomForNewKey(keysInScope, _maxKeys);

        var candidate = PluginDataEntry.Create(_pluginId, _clientRef, key, value);
        _context.PluginDataEntries.Add(candidate);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another call wrote this key first, between the lookup and the insert. Drop the rejected candidate so the
            // next save does not retry it, then write over the winner: the later write wins, as for an existing key.
            _context.Entry(candidate).State = EntityState.Detached;
            var winner = await Scoped(key).FirstAsync(ct);
            winner.ChangeValue(value);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string key, CancellationToken ct)
    {
        PluginDataEntry.EnsureKey(key);
        var existing = await Scoped(key).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return false;
        }

        _context.PluginDataEntries.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Every row in this store's plugin and client scope, whatever its key.</summary>
    /// <returns>The query; tracked unless the caller asks otherwise.</returns>
    private IQueryable<PluginDataEntry> Scope() => _context.PluginDataEntries
        .Where(row => row.PluginId == _pluginId && row.ClientRef == _clientRef);

    /// <summary>The row of one key in this store's plugin and client scope.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The query; tracked unless the caller asks otherwise.</returns>
    private IQueryable<PluginDataEntry> Scoped(string key) => Scope().Where(row => row.Key == key);
}
